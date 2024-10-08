﻿using Chatbot;

namespace Chatbot
{
    public static class Settings
    {
        public static string Language { get; set; }
        public static string PromptTag { get; set; }
        public static string MessageTag { get; set; }

        public static BlobLogs BlobLogs { get; set; }

        public static int MaxWordSizePerTurn { get; set; }


    }
}
