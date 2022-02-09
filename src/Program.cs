// See https://aka.ms/new-console-template for more information
using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Web;
using System.Collections.Generic;

namespace smart_local
{
    /// <summary>
    /// Main Program
    /// </summary>
    public static class Program
    {
        private const string _clientId = "fhir_demo_id";
        private const string _defaultFhirServerUrl 
        ="https://launch.smarthealthit.org/v/r4/sim/eyJoIjoiMSIsImUiOiJlNDQzYWM1OC04ZWNlLTQzODUtOGQ1NS03NzVjMWI4ZjNhMzcifQ/fhir/";
        
        private static string _authCode = string.Empty;
        private static string _clientState = string.Empty;
        private static string _redirectUrl = string.Empty;
        private static string _tokenUrl = string.Empty;

        /// <summary>
        /// Program to access a SMART FHIR Server with a local webserver for redirection
        /// </summary>
        /// <param name="fhirServerUrl">FHIR R4 endpoint URL</param>
        /// <returns></returns>
        static int Main (
            string fhirServerUrl)
        {
            if (string.IsNullOrEmpty(fhirServerUrl))
            {
                fhirServerUrl = _defaultFhirServerUrl;
            }
            System.Console.WriteLine($" FHIR Server : {fhirServerUrl} ");

            Hl7.Fhir.Rest.FhirClient fhirClient = new Hl7.Fhir.Rest.FhirClient(fhirServerUrl);
            
            if(!FhirUtils.TryGetSmartUrls(fhirClient, out string authorizeUrl, out string tokenUrl))
            {
                System.Console.WriteLine("Failed to get SMART URLs");
                return -1;
            }
            
            System.Console.WriteLine($"Authorize URL : {authorizeUrl}");
            
            System.Console.WriteLine($"    Token URL : {tokenUrl}");
            _tokenUrl = tokenUrl;

            Task.Run(() => CreateHostBuilder().Build().Run());
            
            int listeningPort = GetListenPort().Result;

            System.Console.WriteLine($" Listening on : {listeningPort}");
            _redirectUrl = $"http://127.0.0.1:{listeningPort}";

            // Location: https://ehr/authorize?
            // response_type=code&
            // client_id=app-client-id&
            // redirect_uri=https%3A%2F%2Fapp%2Fafter-auth&
            // launch=xyz123&
            // scope=launch+patient%2FObservation.rs+patient%2FPatient.rs+openid+fhirUser&
            // state=98wrghuwuogerg97&
            // aud=https://ehr/fhir

            //http://127.0.0.1:56592/?code=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJjb250ZXh0Ijp7Im5lZWRfcGF0aWVudF9iYW5uZXIiOnRydWUsInNtYXJ0X3N0eWxlX3VybCI6Imh0dHBzOi8vbGF1bmNoLnNtYXJ0aGVhbHRoaXQub3JnL3NtYXJ0LXN0eWxlLmpzb24iLCJwYXRpZW50IjoiMmNkYTVhYWQtZTQwOS00MDcwLTlhMTUtZTFjMzVjNDZlZDVhIn0sImNsaWVudF9pZCI6ImZoaXJfZGVtb19pZCIsInNjb3BlIjoib3BlbmlkIGZoaXJVc2VyIHByb2ZpbGUgbGF1bmNoL3BhdGllbnQgcGF0aWVudC8qLnJlYWQiLCJ1c2VyIjoiUHJhY3RpdGlvbmVyL2U0NDNhYzU4LThlY2UtNDM4NS04ZDU1LTc3NWMxYjhmM2EzNyIsInJlZGlyZWN0X3VyaSI6Imh0dHA6Ly8xMjcuMC4wLjE6NTY1OTIiLCJpYXQiOjE2NDQxOTM2MjksImV4cCI6MTY0NDE5MzkyOX0.SywprQWM7DdJXYxLMZ8iHeFFm7DPBlre-7d_k0q6l2o&state=local_state
            string url =
                $"{authorizeUrl}" +
                $"?response_type=code" +
                $"&client_id={_clientId}" +
                $"&redirect_uri={HttpUtility.UrlEncode(_redirectUrl)}" +
                $"&scope={HttpUtility.UrlEncode("openid fhirUser profile launch/patient patient/*.read")}" +
                $"&state=local_state" +
                $"&aud={fhirServerUrl}";

            LaunchUrl(url);

            for (int loops = 0; loops < 30; loops++)
            {
                System.Threading.Thread.Sleep(1000);
            }
            return 0;
        }

        /// <summary>
        /// Set authorization code and state
        /// </summary>
        /// <param name="code"></param>
        /// <param name="state"></param>
        public static async void SetAuthCode(string code, string state)
        {
            _authCode = code;
            _clientState = state;    

            System.Console.WriteLine($"Code received: {code}");    

            Dictionary<string, string> requestValues = new Dictionary<string, string>()
            {
                { "grant_type","authorization_code" },
                { "code",code },
                { "redirect_uri",_redirectUrl },
                { "client_id",_clientId },                                            
            };

            HttpRequestMessage request =  new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_tokenUrl),
                Content = new FormUrlEncodedContent(requestValues),

            };     

            HttpClient client = new HttpClient();

            HttpResponseMessage response = await client.SendAsync(request);    
        
            if (!response.IsSuccessStatusCode)
            {
                System.Console.WriteLine($"Failed to exchange code for token.");
                throw new Exception($"Unauthorized: {response.StatusCode}");
            }

            string json = await response.Content.ReadAsStringAsync();
            System.Console.WriteLine($"/==== Token ====/");
            System.Console.WriteLine(json);
            System.Console.WriteLine($"/==== Token ====/");
        }

        /// <summary>
        ///  Launch URL in the user's default browser.
        /// </summary>
        /// <param name="url"></param>
        /// <returns>true if successful, otherwise false.</returns>
        public static bool LaunchUrl(string url)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = url,
                    UseShellExecute = true,
                };

                Process.Start(startInfo);    
                return true;
            }
            catch (Exception)
            {
                // ignore
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true});
                    return true;
                }
                catch (Exception)
                {
                    // ignore
                }

            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string[] allowedProgramsToRun = { "xdg-open", "gnome-open", "kfmclient" };

                foreach (string helper in allowedProgramsToRun)
                {
                    try 
                    {
                        Process.Start(helper,url);
                        return true;
                    }
                    catch (Exception)
                    {
                        // ignore
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try 
                {
                    Process.Start("open",url);
                    return true;
                }
                catch (Exception)
                {
                    System.Console.WriteLine($"Failed to Lunch URL");
                    return false;
                }

            }

            return true;
        }

        /// <summary>
        /// Determine the listerning port for the webserver
        /// </summary>
        /// <returns></returns>
        public static async Task<int> GetListenPort()
        {
            for (int loops = 0; loops < 100; loops++)
            {
                await Task.Delay(100);
                
                if(Startup.Addresses == null)
                {
                    continue;
                }
                string address = Startup.Addresses.Addresses.FirstOrDefault();

                if (string.IsNullOrEmpty(address))
                {
                    continue;
                }

                if(address.Length < 18)
                {
                    continue;
                }

                if (int.TryParse(address.Substring(17), out int port) && port != 0)
                {
                    return port;    
                }
            }
            throw new Exception($"Failed to get a listening port!");
        }

         public static IHostBuilder CreateHostBuilder() =>
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("http://127.0.0.1:0");
                    webBuilder.UseKestrel();
                    webBuilder.UseStartup<Startup>();
                });

    }
}


