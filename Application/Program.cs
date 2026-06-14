using System;
using System.Net;
using System.IO;
using System.Threading;

class Program
{
    static async Task Main(string[] args)
    {
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5000/");

        bool serverRunning = true;

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n[SERVER] Gašenje...");
            serverRunning = false;
            listener.Stop();
        };

        try
        {
            listener.Start();
            Console.WriteLine("Server pokrenut na http://localhost:5000/");

            while (serverRunning)
            {
                try
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    //ThreadPool.QueueUserWorkItem(ProcessRequest, context);
                    _ = ProcessRequest(context);
                }
                catch (Exception ex) when (!serverRunning)
                {
                    Console.WriteLine("[SERVER] Slušalac zaustavljen.");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Greška: " + ex.Message);
        }
        finally
        {
            listener.Close();
            Console.WriteLine("[SERVER] Server zaustavljen.");
        }
    }

    private static Task ProcessRequest(HttpListenerContext context)
    {
        var request = context.Request;

        if (request.Url.AbsolutePath.EndsWith("favicon.ico"))
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.Close();
            return Task.CompletedTask;
        }

        string category = request.QueryString["category"];
        string difficulty = request.QueryString["difficulty"]?.Trim().ToLower();

        Task<string> quizDataTask = ApiService.GetQuizData(category, difficulty);

        return quizDataTask.ContinueWith(antecedentTask =>
        {
            try
            {
                if (antecedentTask.IsFaulted)
                {
                    Exception ex = antecedentTask.Exception.InnerException ?? antecedentTask.Exception;
                    SendResponse(context, $"{{\"error\": \"{ex.Message}\"}}", HttpStatusCode.InternalServerError);
                }
                else if (antecedentTask.IsCompletedSuccessfully)
                {
                    string result = antecedentTask.Result;
                    SendResponse(context, result, HttpStatusCode.OK);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONTINUE WITH] Greška unutar kontinuacije: {ex.Message}");
                context.Response.Close();
            }
        });
    }

    private static void SendResponse(HttpListenerContext context, string content, HttpStatusCode status)
    {
        try
        {
            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/json";
            using var writer = new StreamWriter(context.Response.OutputStream);
            writer.Write(content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Greška pri slanju odgovora: {ex.Message}");
        }
        finally
        {
            context.Response.Close();
        }
    }
}