using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace ADO_Tools_WinUI.Services
{
    public sealed class LocalEmbeddingService : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly BertTokenizer _tokenizer;
        private const int MaxTokens = 256;
        private const int ChunkOverlapTokens = 32;
        private const int InferenceBatchSize = 32;

        /// <summary>
        /// True if the ONNX session is running on GPU via DirectML.
        /// </summary>
        public bool IsUsingGpu { get; }

        public LocalEmbeddingService(string modelDir)
        {
            string modelPath = Path.Combine(modelDir, "model.onnx");
            string vocabPath = Path.Combine(modelDir, "vocab.txt");

            InferenceSession? session = null;
            bool usingGpu = false;

            try
            {
                var gpuOptions = new SessionOptions();
                gpuOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                gpuOptions.AppendExecutionProvider_DML(0);
                session = new InferenceSession(modelPath, gpuOptions);
                usingGpu = true;
            }
            catch
            {
                // DirectML not available — fall back to CPU
                var cpuOptions = new SessionOptions();
                cpuOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                session = new InferenceSession(modelPath, cpuOptions);
            }

            _session = session;
            IsUsingGpu = usingGpu;
            _tokenizer = BertTokenizer.Create(vocabPath);
        }

        public float[] GetEmbedding(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new float[384];

            var ids = _tokenizer.EncodeToIds(text, MaxTokens, out _, out _);
            return EmbedTokenIds(ids);
        }

        /// <summary>
        /// Produces multiple embeddings for long text by chunking tokens with overlap.
        /// Each chunk is MaxTokens long (including [CLS]/[SEP] overhead).
        /// Returns one embedding per chunk so callers can match against the best one.
        /// </summary>
        public List<float[]> GetChunkedEmbeddings(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return [new float[384]];

            var allIds = _tokenizer.EncodeToIds(text, int.MaxValue, out _, out _);

            if (allIds.Count <= MaxTokens)
                return [EmbedTokenIds(allIds)];

            var contentIds = allIds.Skip(1).Take(allIds.Count - 2).ToList();
            int contentMaxPerChunk = MaxTokens - 2;
            int stride = contentMaxPerChunk - ChunkOverlapTokens;
            if (stride < 1) stride = 1;

            var allChunks = new List<IReadOnlyList<int>>();
            for (int offset = 0; offset < contentIds.Count; offset += stride)
            {
                var chunkContent = contentIds.Skip(offset).Take(contentMaxPerChunk).ToList();
                var chunkIds = new List<int> { 101 };
                chunkIds.AddRange(chunkContent);
                chunkIds.Add(102);
                allChunks.Add(chunkIds);
            }

            return EmbedBatch(allChunks);
        }

        /// <summary>
        /// Embeds multiple texts in batched inference calls for better GPU utilization.
        /// Returns one list of chunk embeddings per input text.
        /// </summary>
        public List<List<float[]>> GetBatchedChunkedEmbeddings(List<string> texts)
        {
            var allChunkGroups = new List<(int TextIndex, List<IReadOnlyList<int>> Chunks)>();

            for (int i = 0; i < texts.Count; i++)
            {
                var text = texts[i];
                if (string.IsNullOrWhiteSpace(text))
                {
                    allChunkGroups.Add((i, [new List<int>()]));
                    continue;
                }

                var allIds = _tokenizer.EncodeToIds(text, int.MaxValue, out _, out _);

                if (allIds.Count <= MaxTokens)
                {
                    allChunkGroups.Add((i, [allIds]));
                    continue;
                }

                var contentIds = allIds.Skip(1).Take(allIds.Count - 2).ToList();
                int contentMaxPerChunk = MaxTokens - 2;
                int stride = contentMaxPerChunk - ChunkOverlapTokens;
                if (stride < 1) stride = 1;

                var chunks = new List<IReadOnlyList<int>>();
                for (int offset = 0; offset < contentIds.Count; offset += stride)
                {
                    var chunkContent = contentIds.Skip(offset).Take(contentMaxPerChunk).ToList();
                    var chunkIds = new List<int> { 101 };
                    chunkIds.AddRange(chunkContent);
                    chunkIds.Add(102);
                    chunks.Add(chunkIds);
                }
                allChunkGroups.Add((i, chunks));
            }

            var flatChunks = new List<IReadOnlyList<int>>();
            var chunkOwner = new List<int>();

            foreach (var (textIndex, chunks) in allChunkGroups)
            {
                foreach (var chunk in chunks)
                {
                    flatChunks.Add(chunk);
                    chunkOwner.Add(textIndex);
                }
            }

            var flatEmbeddings = EmbedBatch(flatChunks);

            var results = new List<List<float[]>>(texts.Count);
            for (int i = 0; i < texts.Count; i++)
                results.Add(new List<float[]>());

            for (int i = 0; i < flatEmbeddings.Count; i++)
                results[chunkOwner[i]].Add(flatEmbeddings[i]);

            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].Count == 0)
                    results[i].Add(new float[384]);
            }

            return results;
        }

        /// <summary>
        /// Runs batched inference on multiple token sequences. Pads shorter sequences
        /// and processes in batches of InferenceBatchSize for optimal GPU throughput.
        /// </summary>
        private List<float[]> EmbedBatch(List<IReadOnlyList<int>> sequences)
        {
            if (sequences.Count == 0) return [];
            if (sequences.Count == 1) return [EmbedTokenIds(sequences[0])];

            var allResults = new List<float[]>();

            for (int batchStart = 0; batchStart < sequences.Count; batchStart += InferenceBatchSize)
            {
                var batch = sequences.Skip(batchStart).Take(InferenceBatchSize).ToList();
                int batchSize = batch.Count;
                int maxLen = batch.Max(s => s.Count);

                var inputIds = new long[batchSize * maxLen];
                var attentionMask = new long[batchSize * maxLen];
                var tokenTypeIds = new long[batchSize * maxLen];

                for (int b = 0; b < batchSize; b++)
                {
                    var seq = batch[b];
                    int offset = b * maxLen;
                    for (int t = 0; t < seq.Count; t++)
                    {
                        inputIds[offset + t] = seq[t];
                        attentionMask[offset + t] = 1;
                    }
                }

                var dims = new[] { batchSize, maxLen };

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input_ids",
                        new DenseTensor<long>(inputIds, dims)),
                    NamedOnnxValue.CreateFromTensor("attention_mask",
                        new DenseTensor<long>(attentionMask, dims)),
                    NamedOnnxValue.CreateFromTensor("token_type_ids",
                        new DenseTensor<long>(tokenTypeIds, dims))
                };

                using var results = _session.Run(inputs);
                var output = results.First().AsTensor<float>();
                int embDim = output.Dimensions[2];

                for (int b = 0; b < batchSize; b++)
                {
                    var seq = batch[b];
                    int seqLen = seq.Count;
                    var embedding = new float[embDim];

                    for (int t = 0; t < seqLen; t++)
                    {
                        for (int d = 0; d < embDim; d++)
                            embedding[d] += output[b, t, d];
                    }

                    for (int d = 0; d < embDim; d++)
                        embedding[d] /= seqLen;

                    float norm = MathF.Sqrt(embedding.Sum(x => x * x));
                    if (norm > 0)
                    {
                        for (int d = 0; d < embDim; d++)
                            embedding[d] /= norm;
                    }

                    allResults.Add(embedding);
                }
            }

            return allResults;
        }

        private float[] EmbedTokenIds(IReadOnlyList<int> ids)
        {
            int seqLen = ids.Count;
            var inputIds = new long[seqLen];
            var attentionMask = new long[seqLen];
            var tokenTypeIds = new long[seqLen];

            for (int i = 0; i < seqLen; i++)
            {
                inputIds[i] = ids[i];
                attentionMask[i] = 1;
            }

            var dims = new[] { 1, seqLen };

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids",
                    new DenseTensor<long>(inputIds, dims)),
                NamedOnnxValue.CreateFromTensor("attention_mask",
                    new DenseTensor<long>(attentionMask, dims)),
                NamedOnnxValue.CreateFromTensor("token_type_ids",
                    new DenseTensor<long>(tokenTypeIds, dims))
            };

            using var results = _session.Run(inputs);

            var output = results.First().AsTensor<float>();
            int embDim = output.Dimensions[2];
            var embedding = new float[embDim];

            for (int t = 0; t < seqLen; t++)
            {
                if (attentionMask[t] == 0) continue;
                for (int d = 0; d < embDim; d++)
                    embedding[d] += output[0, t, d];
            }

            for (int d = 0; d < embDim; d++)
                embedding[d] /= seqLen;

            float norm = MathF.Sqrt(embedding.Sum(x => x * x));
            if (norm > 0)
            {
                for (int d = 0; d < embDim; d++)
                    embedding[d] /= norm;
            }

            return embedding;
        }

        public static float CosineSimilarity(float[] a, float[] b)
        {
            float dot = 0;
            for (int i = 0; i < a.Length; i++)
                dot += a[i] * b[i];
            return dot;
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
