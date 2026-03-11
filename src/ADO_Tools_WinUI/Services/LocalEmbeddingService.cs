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

        public LocalEmbeddingService(string modelDir)
        {
            string modelPath = Path.Combine(modelDir, "model.onnx");
            string vocabPath = Path.Combine(modelDir, "vocab.txt");

            var options = new SessionOptions();
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            _session = new InferenceSession(modelPath, options);
            _tokenizer = BertTokenizer.Create(vocabPath);
        }

        public float[] GetEmbedding(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new float[384];

            // BertTokenizer.EncodeToIds adds [CLS] and [SEP] automatically
            var ids = _tokenizer.EncodeToIds(text, MaxTokens, out _, out _);

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

            // Model output: last_hidden_state [1, seqLen, 384]
            // Mean pooling over token dimension
            var output = results.First().AsTensor<float>();
            int embDim = output.Dimensions[2];
            var embedding = new float[embDim];

            for (int t = 0; t < seqLen; t++)
            {
                if (attentionMask[t] == 0) continue;
                for (int d = 0; d < embDim; d++)
                    embedding[d] += output[0, t, d];
            }

            int tokenCount = seqLen; // All tokens have attention_mask = 1
            for (int d = 0; d < embDim; d++)
                embedding[d] /= tokenCount;

            // L2 normalize
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
