// See https://aka.ms/new-console-template for more information
using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.Linq;

namespace smart_local
{
    /// <summary>
    /// Main Program
    /// </summary>
    public static class Program
    {
        private const string _defaultFhirServerUrl 
        ="https://launch.smarthealthit.org/v/r4/sim/eyJoIjoiMSIsImUiOiJlNDQzYWM1OC04ZWNlLTQzODUtOGQ1NS03NzVjMWI4ZjNhMzcifQ/fhir/";
        
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

            Task.Run(() => CreateHostBuilder().Build().Run());
            
            int listeningPort = GetListenPort().Result;

            System.Console.WriteLine($" Listening on : {listeningPort}");

            for (int loops = 0; loops < 5; loops++)
            {
                System.Threading.Thread.Sleep(1000);
            }
            return 0;
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


