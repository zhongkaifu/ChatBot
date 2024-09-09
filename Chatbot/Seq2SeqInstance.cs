﻿// Copyright (c) Zhongkai Fu. All rights reserved.
// https://github.com/zhongkaifu/Seq2SeqSharp
//
// This file is part of Seq2SeqSharp.
//
// Seq2SeqSharp is licensed under the BSD-3-Clause license found in the LICENSE file in the root directory of this source tree.
//
// Seq2SeqSharp is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the BSD-3-Clause License for more details.

using Seq2SeqSharp;
using Seq2SeqSharp.Corpus;
using Seq2SeqSharp._SentencePiece;
using Seq2SeqSharp.Applications;
using AdvUtils;
using Seq2SeqSharp.Utils;
using Seq2SeqSharp.Tools;
using TensorSharp.CUDA.ContextState;
using Microsoft.Extensions.Logging;

namespace Chatbot
{
    public enum ModelType
    {
        EncoderDecoder,
        DecoderOnly
    }

    public static class Seq2SeqInstance
    {
        static private BaseSeq2SeqFramework<Seq2SeqModel>? m_seq2seq;
        static private SentencePiece? m_srcSpm = null;
        static private SentencePiece? m_tgtSpm = null;
        static private Seq2SeqOptions? opts;
        static Semaphore? sm = null;
        static List<int>? m_blockedTokens = null;
        static ModelType m_modelType = ModelType.EncoderDecoder;

        static public int MaxTokenToGenerate;
        static public int MaxSrcSentLength => opts.MaxSrcSentLength;
        static public int MaxTgtSentLength => opts.MaxTgtSentLength;

        static object locker = new object();

        static public void Initialization(string modelFilePath, int maxTestSrcSentLength, int maxTestTgtSentLength, int maxTokenToGeneration, ProcessorTypeEnums processorType, string deviceIds, SentencePiece? srcSpm, SentencePiece? tgtSpm,
            Seq2SeqSharp.Utils.DecodingStrategyEnums decodingStrategyEnum, float memoryUsageRatio, string mklInstructions, int beamSearchSize, string blockedTokens, ModelType modelType,
            string wordMappingFilePath, bool enableTensorCore, string compilerOptions, bool amp, CudaMemoryDeviceAllocatorType cudaMemoryDeviceAllocatorType, AttentionTypeEnums attentionType)
        {
            opts = new Seq2SeqOptions();
            opts.ModelFilePath = modelFilePath;
            opts.MaxSrcSentLength = maxTestSrcSentLength;
            opts.MaxTgtSentLength = maxTestTgtSentLength;
            opts.ProcessorType = processorType;
            opts.DeviceIds = deviceIds;
            opts.DecodingStrategy = decodingStrategyEnum;
            opts.MemoryUsageRatio = memoryUsageRatio;
            opts.MKLInstructions = mklInstructions;
            opts.BeamSearchSize = beamSearchSize;
            opts.EnableTensorCore = enableTensorCore;
            opts.CompilerOptions = compilerOptions;
            opts.AMP = amp;
            opts.IsDecoderTrainable = false;
            opts.IsEncoderTrainable = false;
            opts.IsSrcEmbeddingTrainable = false;
            opts.IsTgtEmbeddingTrainable = false;
            opts.CudaMemoryAllocatorType = cudaMemoryDeviceAllocatorType;
            opts.RandomSeed = 1234;
            opts.AttentionType = attentionType;

            MaxTokenToGenerate = maxTokenToGeneration;

            m_srcSpm = srcSpm;
            m_tgtSpm = tgtSpm;
            m_modelType = modelType;

            if (opts.ProcessorType != ProcessorTypeEnums.GPU)
            {
                sm = new Semaphore(Environment.ProcessorCount, Environment.ProcessorCount);
            }
            else
            {
                sm = new Semaphore(1, 1);
            }

            try
            {
                Logger.WriteLine($"Creating Seq2Seq instance. ModelType = '{m_modelType}'");
                if (m_modelType == ModelType.EncoderDecoder)
                {
                    m_seq2seq = new Seq2Seq(opts);
                }
                else
                {
                    m_seq2seq = new GPT(opts);
                }
                if (String.IsNullOrEmpty(blockedTokens) == false)
                {
                    Logger.WriteLine($"Creating blocked tokens = '{blockedTokens}'");
                    string[] tokens = blockedTokens.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    m_blockedTokens = new List<int>();
                    foreach (var token in tokens)
                    {
                        m_blockedTokens.Add(int.Parse(token));
                    }
                }
            }
            catch (Exception e)
            {
                Logger.WriteLine($"Error: '{e.Message}'");
                Logger.WriteLine($"Call Stack: '{e.StackTrace}'");
                throw;
            }

        }

        static public string Call(string rawSrcInput, string rawTgtInput, int tokenNumToGenerate, float topP, float temperature, float repeatPenalty)
        {
            bool startWithSpace = false;
            bool endWithSpace = false;

            if (rawSrcInput.StartsWith(" "))
            {
                startWithSpace = true;
            }

            if (rawSrcInput.EndsWith(" "))
            {
                endWithSpace = true;
            }

            rawSrcInput = rawSrcInput.Trim();
            rawTgtInput = rawTgtInput.Trim();

            if (opts == null)
            {
                throw new ArgumentNullException($"The {nameof(Seq2SeqInstance)} may not be initialized, and option instance is null.");
            }

            if (m_seq2seq == null)
            {
                throw new ArgumentNullException($"The {nameof(Seq2SeqInstance)} is null.");
            }

            if (m_modelType == ModelType.DecoderOnly && String.IsNullOrEmpty(rawTgtInput))
            {
                rawTgtInput = rawSrcInput;
            }

            var srcInput = (m_srcSpm != null) ? m_srcSpm.Encode(rawSrcInput) : rawSrcInput;

            List<string> srcTokens = srcInput.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            List<List<String>> srcBatchTokens = new List<List<string>>();
            srcBatchTokens.Add(srcTokens);

            var tgtInput = (m_tgtSpm != null) ? m_tgtSpm.Encode(rawTgtInput) : rawTgtInput;

            List<string> tgtTokens = tgtInput.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            tokenNumToGenerate += tgtTokens.Count;

            List<List<String>> tgtBatchTokens = new List<List<string>>();
            tgtBatchTokens.Add(tgtTokens);

            DecodingOptions decodingOptions = opts.CreateDecodingOptions();
            decodingOptions.MaxTgtSentLength = tokenNumToGenerate;
            decodingOptions.BlockedTokens = m_blockedTokens;
            decodingOptions.TopP = topP;
            decodingOptions.Temperature = temperature;
            decodingOptions.RepeatPenalty = repeatPenalty;

            if (temperature == 0.0f)
            {
                decodingOptions.DecodingStrategy = DecodingStrategyEnums.GreedySearch;
            }
            else
            {
                decodingOptions.DecodingStrategy = DecodingStrategyEnums.Sampling;
            }

            try
            {
                sm?.WaitOne();

                List<NetworkResult> nrs = null;

                if (m_modelType == ModelType.EncoderDecoder)
                {
                    nrs = m_seq2seq.Test<Seq2SeqCorpusBatch>(srcBatchTokens, tgtBatchTokens, decodingOptions);
                }
                else
                {
                    nrs = m_seq2seq.Test<SeqCorpusBatch>(tgtBatchTokens, tgtBatchTokens, decodingOptions);
                }

                string rst = String.Join(" ", nrs[0].Output[0][0].ToArray(), 0, nrs[0].Output[0][0].Count);
                bool isEnded = (rst.EndsWith("</s>") || rst == tgtInput || (nrs[0].Status == NetworkResultStatus.OOM && rst == tgtInput));
                rst = (m_tgtSpm != null) ? m_tgtSpm.Decode(rst) : rst;
                if (isEnded)
                {
                    rst += " EOS";
                }


                if (startWithSpace)
                {
                    rst = " " + rst;
                }

                if (endWithSpace)
                {
                    rst = rst + " ";
                }

                return rst;
            }
            catch (Exception ex)
            {
                Logger.WriteLine(Logger.Level.err, $"Error Message = '{ex.Message}', Call stack = '{ex.StackTrace}'");

                return rawTgtInput + " EOS";
            }
            finally
            {
                sm?.Release();
            }
        }
    }
}
