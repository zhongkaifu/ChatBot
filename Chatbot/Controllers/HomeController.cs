using Chatbot.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Diagnostics;
using AdvUtils;
using TensorSharp.CUDA.DeviceCode;
using System.Net;
using System.Diagnostics.CodeAnalysis;
using Azure.Core;

namespace Chatbot.Controllers
{
    [Serializable]
    public class BackendResult
    {
        public string? Output { get; set; }
    }

    public static class SessionExtensions
    {
        public static void SetObject(this ISession session, string key, object value)
        {
            session.SetString(key, JsonConvert.SerializeObject(value));
        }

        public static T GetObject<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default(T) : JsonConvert.DeserializeObject<T>(value);
        }
    }

    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public IActionResult RemoveTurn(string transcript, int idx)
        {          
            transcript = Utils.RemoveHtmlTags(transcript);
            List<string> turnText = Utils.SplitTurns(transcript);

            turnText = turnText.GetRange(0, idx);
            BackendResult tr = new BackendResult
            {
                Output = String.Join("</div>", Utils.AddHtmlTags(turnText))
            };

            return new JsonResult(tr);
        }

        [HttpPost]
        public IActionResult RefreshTurn(string transcript, bool contiGen, int idx)
        {
            transcript = Utils.RemoveHtmlTags(transcript);
            List<string> turnText = Utils.SplitTurns(transcript);

            if (turnText.Count == 1)
            {
                BackendResult tr2 = new BackendResult
                {
                    Output = String.Join("</div>", Utils.AddHtmlTags(turnText))
                };

                return new JsonResult(tr2);
            }

            if (contiGen == false)
            {
                turnText = turnText.GetRange(0, idx);
                turnText.Add($"{Settings.MessageTag}");
            }

            turnText = CallInHouseModel(turnText, 0.1f, 1.0f, 0.0f);
            var remoteIpAddress = Request.HttpContext.Connection.RemoteIpAddress;
            string rawOutput = String.Join("", turnText);
            string logLine = $"Client '{remoteIpAddress.ToString()}' Regenerate Turn: '{rawOutput}'";
            Logger.WriteLine(logLine);
            if (rawOutput.EndsWith(" EOS"))
            {
                Settings.BlobLogs.WriteLine(logLine);
            }
            BackendResult tr = new BackendResult
            {
                Output = String.Join("</div>", Utils.AddHtmlTags(turnText))
            };

            return new JsonResult(tr);
        }

        [HttpPost]
        public IActionResult SendTurn(string transcript, string inputTurn, bool contiGen)
        {
            if (contiGen == false && String.IsNullOrEmpty(inputTurn))
            {
                BackendResult tr2 = new BackendResult
                {
                    Output = transcript
                };

                return new JsonResult(tr2);
            }

            transcript = Utils.RemoveHtmlTags(transcript);
            List<string> turnText = Utils.SplitTurns(transcript);

            if (contiGen == false)
            {
                turnText.Add($"{Settings.PromptTag}" + inputTurn);
                turnText.Add($"{Settings.MessageTag}");
            }

            // Call model to generate outputs
            turnText = CallInHouseModel(turnText, 0.0f, 0.0f, 0.0f);
            
            var remoteIpAddress = Request.HttpContext.Connection.RemoteIpAddress;
            string rawOutput = String.Join("", turnText);
            string logLine = $"Client '{remoteIpAddress.ToString()}' New Turn: '{rawOutput}'";
            Logger.WriteLine(logLine);
            if (rawOutput.EndsWith(" EOS"))
            {
                Settings.BlobLogs.WriteLine(logLine);
            }
            BackendResult tr = new BackendResult
            {
                Output = String.Join("</div>", Utils.AddHtmlTags(turnText))
            };

            return new JsonResult(tr);
        }

        public bool IsRepeatTurn(string turn)
        {
            var results = Utils.FindRepeatingSubstrings(turn);

            foreach (var pair in results)
            {
                if (pair.Key.Length >= 3 && pair.Value >= 2)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Call in-house chat model
        /// </summary>
        /// <param name="turnText"></param>
        /// <param name="topP"></param>
        /// <param name="aiDoctor"></param>
        /// <param name="nbest"></param>
        /// <returns></returns>
        private List<string> CallInHouseModel(List<string> turnText, float topP, float temperature, float penaltyScore = 1.0f)
        {
            string inputText = String.Join("", turnText);


            // Ask model to generate a completed turn
            string outputText = Seq2SeqInstance.Call(inputText, inputText, 8, topP, temperature, penaltyScore);

            if (IsRepeatTurn(outputText))
            {
                Logger.WriteLine(Logger.Level.warn, $"Find repeated output = '{outputText}'");
                outputText = inputText + " EOS";
            }

            if (outputText.EndsWith(Settings.PromptTag))
            {
                outputText = outputText + " EOS";
            }
            if (outputText.EndsWith(Settings.MessageTag))
            {
                outputText = outputText + " EOS";
            }

            var newTurns = Utils.SplitTurns(outputText);
            if (newTurns.Count < turnText.Count)
            {
                return turnText;
            }

            if (newTurns.Count > turnText.Count)
            {
                newTurns[turnText.Count - 1] = TruncateTurn(newTurns[turnText.Count - 1]);
                newTurns[turnText.Count - 1] += " EOS";
            }
            else if (newTurns[^1].Length >= Settings.MaxWordSizePerTurn)
            {
                newTurns[^1] = TruncateTurn(newTurns[^1]);
                newTurns[^1] += " EOS";
            }

            newTurns = newTurns.GetRange(0, turnText.Count);

            return newTurns;
        }


        private static string[] TruncateChars = { "?", "）", "。", "！", "”", ")", "..." };
        private static string TruncateTurn(string turn)
        {
            int truncateIdx = -1;
            foreach (var tchar in TruncateChars)
            {
                int idx = turn.LastIndexOf(tchar);
                truncateIdx = Math.Max(truncateIdx, idx);
            }

            if (truncateIdx >= 0)
            {
                turn = turn.Substring(0, truncateIdx + 1);
            }

            return turn;
        }
    }
}