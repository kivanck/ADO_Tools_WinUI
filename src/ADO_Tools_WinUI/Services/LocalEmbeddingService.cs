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

        /// <summary>
        /// True if the ONNX session is running on GPU via DirectML.
        /// </summary>
        public bool IsUsingGpu { get; }

        public LocalEmbeddingService(string modelDir)
        {
            string modelPath = Path.Combine(modelDir, "model.onnx");
            string vocabPath = Path.Combine(modelDir, "vocab.txt");

            // Try DirectML (GPU) first, fall back to CPU if unavailable
            InferenceSession? session = null;
            bool usingGpu = false;

            try
            {
                var gpuOptions = new SessionOptions();
                gpuOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                gpuOptions.AppendExecutionProvider_DML(0); // device 0 = default GPU
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

            // BertTokenizer.EncodeToIds adds [CLS] and [SEP] automatically
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

            // Encode the full text without truncation limit
            var allIds = _tokenizer.EncodeToIds(text, int.MaxValue, out _, out _);

            // If it fits in a single chunk, just return one embedding
            if (allIds.Count <= MaxTokens)
                return [EmbedTokenIds(allIds)];

            // The tokenizer adds [CLS] (101) at start and [SEP] (102) at end.
            // Strip them so we can re-chunk the "content" tokens, then re-add per chunk.
            var contentIds = allIds.Skip(1).Take(allIds.Count - 2).ToList();
            int contentMaxPerChunk = MaxTokens - 2; // room for [CLS] and [SEP]
            int stride = contentMaxPerChunk - ChunkOverlapTokens;
            if (stride < 1) stride = 1;

            var results = new List<float[]>();
            for (int offset = 0; offset < contentIds.Count; offset += stride)
            {
                var chunkContent = contentIds.Skip(offset).Take(contentMaxPerChunk).ToList();
                // Rebuild a full token sequence: [CLS] + chunk + [SEP]
                var chunkIds = new List<int> { 101 };
                chunkIds.AddRange(chunkContent);
                chunkIds.Add(102);
                results.Add(EmbedTokenIds(chunkIds));
            }

            return results;
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
            return dot; // Already normalized, so dot product = cosine similarity
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
