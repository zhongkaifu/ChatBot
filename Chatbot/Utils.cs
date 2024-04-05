using ProtoBuf.Meta;
using System.Net;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace MedChat
{
    public static class Utils
    {
        public static List<string> SplitTurns(string transcript)
        {
            List<string> turns = new List<string>();

            string[] parts = transcript.Split(Settings.PromptTag, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                string sents = Settings.PromptTag + part;
                string[] tt = sents.Split(Settings.MessageTag, StringSplitOptions.RemoveEmptyEntries);
                foreach (var t in tt)
                {
                    if (t.StartsWith(Settings.PromptTag) == false)
                    {
                        string turn = Settings.MessageTag + t;
                        turns.Add(turn);
                    }
                    else
                    {
                        turns.Add(t);
                    }
                }

            }

            return turns;
        }
        
        public static List<string> AddHtmlTags(List<string> lines)
        {
            List<string> newLines = new List<string>();
            foreach (string line in lines)
            {
                string newLine = "";
                if (line.Contains(Settings.MessageTag))
                {
                    newLine = $"<img src=\"/images/writer.jpg\" width=\"50\">" + line.Replace(Settings.MessageTag, $"<div class=\"ai-message\">");
                }
                else
                {
                    newLine = line.Replace(Settings.PromptTag, $"<div class=\"user-message\">") + "<img src=\"/images/reader.jpg\" width=\"50\">";
                }

                newLine = newLine.Replace("\n", "\n<br>");
                newLine = newLine.Replace("lf1", " <br>");
                newLine = newLine.Replace("lf2", " <br><br>");
                newLines.Add(newLine);
            }

            return newLines;
        }

        public static string RemoveHtmlTags(string input)
        {
            if (String.IsNullOrEmpty(input))
            {
                return "";
            }

            input = input.Replace("<br><br>", "lf2");
            input = input.Replace("<br>", "lf1");

            input = input.Replace($"<div class=\"user-message\">", Settings.PromptTag).Replace($"<div class=\"ai-message\">", Settings.MessageTag);
            return Regex.Replace(input, "<.*?>", String.Empty);
        }
    }
}
