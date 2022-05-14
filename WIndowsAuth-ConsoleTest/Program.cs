using System;
using System.Net;
using System.Net.Http;
using System.Security.Principal;
using System.Text;

namespace WIndowsAuth_ConsoleTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Program.Test();
        }

        private static void Test()
        {
            var url = "http://localhost:8042/About";

            try
            {
                string userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                Console.WriteLine(userName);

                var c = new CredentialCache();
                c.Add(new Uri(url), "Negotiate", new NetworkCredential("test", "test@1234"));
                var clHandler = new HttpClientHandler
                {
                    Credentials = c
                };
                HttpClient client = new HttpClient(clHandler);
                var req = new HttpRequestMessage
                {
                    Content = new StringContent("", Encoding.UTF8, "application/json"),
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(url)
                };
                
                var resp = client.SendAsync(req).Result;
                Console.WriteLine(resp);
                Console.ReadLine();
            }
            catch (AggregateException ex)
            {
                // get all possible exceptions which are thrown
                foreach (var item in ex.Flatten().InnerExceptions)
                {
                    Console.WriteLine(item.Message);
                    Console.ReadLine();
                }
            }
        }
    }
}
