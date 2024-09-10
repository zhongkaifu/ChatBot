using ProtoBuf.Meta;
using System.Net;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace Chatbot
{
    public static class Utils
    {
        public static Dictionary<string, int> FindRepeatingSubstrings(string input)
        {
            Dictionary<string, int> result = new Dictionary<string, int>();
            for (int length = 1; length <= input.Length / 2; length++)
            {
                for (int start = 0; start <= input.Length - 2 * length; start++)
                {
                    string substring = input.Substring(start, length);
                    string nextSubstring = input.Substring(start + length, length);
                    if (substring == nextSubstring)
                    {
                        int count = 2;
                        while (start + count * length + length <= input.Length && input.Substring(start + count * length, length) == substring)
                        {
                            count++;
                        }
                        if (!result.ContainsKey(substring) || count > result[substring])
                        {
                            result[substring] = count;
                        }
                    }
                }
            }
            return result;
        }
        
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
            int turnIdx = 0;
            foreach (string line in lines)
            {
                string newLine = "";
                if (line.Contains(Settings.MessageTag))
                {
                    //newLine = $"<img src=\"/images/writer.jpg\" width=\"50\">" + line.Replace(Settings.MessageTag, $"<div class=\"ai-message\">");
                    newLine = line.Replace(Settings.MessageTag, $"<div class=\"ai-message\"><button class=\"hidden-button-ai\" type=\"button\" title=\"Refresh\" id=\"refreshTurn" + turnIdx.ToString() + "\" onclick=\"RefreshTurn(" + turnIdx.ToString() + ")\"><img src=\"/images/refresh.png\" width=\"10\"></img></button><img src=\"/images/writer.jpg\" width=\"50\"></img>");
                }
                else
                {
                    newLine = line.Replace(Settings.PromptTag, $"<div class=\"user-message\">") + "<img src=\"/images/reader.jpg\" width=\"50\"></img><button class=\"hidden-button-user\" type=\"button\" title=\"Remove\" id=\"removeTurn" + turnIdx.ToString() + "\" onclick=\"RemoveTurn(" + turnIdx.ToString() + ")\"><img src=\"/images/trashbin.png\" width=\"10\"></img></button>";
                }

                newLine = newLine.Replace("\n", "\n<br>");
                newLine = newLine.Replace("__ lf1__", " <br>");
                newLine = newLine.Replace("__ lf2__", " <br><br>");
                newLine = newLine.Replace("__lf1__", " <br>");
                newLine = newLine.Replace("__lf2__", " <br><br>");
                newLine = newLine.Replace("[lf1]", " <br>");
                newLine = newLine.Replace("[lf2]", " <br><br>");
                newLine = newLine.Replace("lf1", " <br>");
                newLine = newLine.Replace("lf2", " <br><br>");



                newLines.Add(newLine);

                turnIdx++;
            }

            return newLines;
        }

        public static string RemoveHtmlTags(string input)
        {
            if (String.IsNullOrEmpty(input))
            {
                return "";
            }

            input = input.Replace("<br><br>", "[lf2]");
            input = input.Replace("<br>", "[lf1]");

            input = input.Replace($"<div class=\"user-message\">", Settings.PromptTag).Replace($"<div class=\"ai-message\">", Settings.MessageTag);
            return Regex.Replace(input, "<.*?>", String.Empty);
        }
    }
}
