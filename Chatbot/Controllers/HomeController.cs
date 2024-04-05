using MedChat.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Diagnostics;
using AdvUtils;
using TensorSharp.CUDA.DeviceCode;
using System.Net;
using System.Diagnostics.CodeAnalysis;

namespace MedChat.Controllers
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
        public IActionResult RegenerateTurn(string transcript, bool contiGen)
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
                turnText.RemoveAt(turnText.Count - 1);
                turnText.Add($"{Settings.MessageTag}");
            }

            turnText = CallInHouseModel(turnText, 0.1f, 1.0f, 1.0f);
            Logger.WriteLine($"Regenerate Turn: '{String.Join("", turnText)}'");
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
            turnText = CallInHouseModel(turnText, 0.0f, 0.0f, 1.0f);
            
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
            var newTurns = Utils.SplitTurns(outputText);
            if (newTurns.Count < turnText.Count)
            {
                return turnText;
            }

            if (newTurns.Count > turnText.Count)
            {
                newTurns[turnText.Count - 1] += " EOS";
            }
            else if (newTurns[^1].Length >= 256)
            {
                int periodIdx = newTurns[^1].LastIndexOf(")");
                if (periodIdx < 0)
                {
                    periodIdx = newTurns[^1].LastIndexOf("。");
                }

                if (periodIdx >= 0)
                {
                    newTurns[^1] = newTurns[^1].Substring(0, periodIdx + 1);
                }

                newTurns[^1] += " EOS";
            }

            newTurns = newTurns.GetRange(0, turnText.Count);

            return newTurns;
        }                  
    }
}