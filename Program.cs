﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks.Dataflow;
using HtmlAgilityPack;
using System.Linq;
using System.Threading.Tasks;

namespace mzitu
{
    class Program
    {
        static readonly string[] CATA = {"japan","taiwan","xinggan","mm"};

        static void Main(string[] args)
        {
            string KEY = CATA[0];
            if (args.Length > 0)
            {
                KEY = CATA[int.Parse(args[0])];
            }
            string RootDir = $@"d:\{KEY}";
            Console.WriteLine("");
            var client = new HttpClient(new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip,
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = 500
                }){Timeout=new TimeSpan(0,3,0)};
            // client.DefaultRequestHeaders.Add("User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/64.0.3282.140 Safari/537.36 Edge/17.17134");

            var f = new TransformManyBlock<string,string>(async page => 
            {
                
                var doc = new HtmlDocument();
                var ret = new List<string>();
                try
                {
                    doc.Load(await client.GetStreamAsync(page));
                    var titles = doc.DocumentNode.SelectNodes("//ul[@id='pins']/li/span/a");

                    foreach (var title in titles)
                    {
                        ret.Add(title.Attributes["href"].Value);
                    }                    
                }
                catch (System.Exception)
                {
                    
                }

                return ret.ToArray();
            }, new ExecutionDataflowBlockOptions{MaxDegreeOfParallelism=Environment.ProcessorCount});
            var s = new TransformManyBlock<string,string>(async ablum => 
            {
                
                var doc = new HtmlDocument();
                var ret = new List<string>();
                try
                {
                    doc.Load(await client.GetStreamAsync(ablum));
                    var tag = doc.DocumentNode.SelectNodes("//div[@class='pagenavi']/a");
                    var count = Int16.Parse(tag[tag.Count - 2].FirstChild.InnerText);

                    for (int i = 1; i <= count; i++)
                    {
                        ret.Add($"{ablum}/{i}");
                    }
                }
                catch (System.Exception)
                {
                    Console.WriteLine($"{ablum} is 301.");
                }
                return ret;

            });
            var d = new ActionBlock<string>(async url =>
            {
                
                var doc = new HtmlDocument();
                try
                {
                    using (var st = await client.GetStreamAsync(url))
                    {
                        doc.Load(st);
                    }

                    var photo = doc.DocumentNode.SelectSingleNode("//div[@class='main-image']/p/a/img");
                    if (photo != null)
                    {
                        var path = new Uri(photo.Attributes["src"].Value).AbsolutePath;
                        var file = path.Substring(1).Replace("/","_");
                        if (true)
                        {
                            if (File.Exists($@"{RootDir}\{file}"))
                            {   
                                Console.WriteLine($@"{photo.Attributes["src"].Value} skip");
                                return;                    

                            }

                            var memory = new MemoryStream();
                            using(var down = new HttpClient{Timeout=new TimeSpan(0,3,0)})
                            {
                                down.DefaultRequestHeaders.Add("Referer", url);
                                using(var hs = await down.GetStreamAsync(photo.Attributes["src"].Value))
                                {                           
                                    await hs.CopyToAsync(memory);
                                }
                            }
                            using (var fs = File.OpenWrite($@"{RootDir}\{file}"))
                            {                          
                                memory.WriteTo(fs);
                                fs.Flush();
                            }
                            Console.WriteLine($@"{photo.Attributes["src"].Value}     ");


                        }
                    }
                }
                catch (System.Exception)
                {
                    
                }

            }, new ExecutionDataflowBlockOptions{MaxDegreeOfParallelism=Environment.ProcessorCount});

            f.LinkTo(s,new DataflowLinkOptions{PropagateCompletion=true});
            s.LinkTo(d, new DataflowLinkOptions{PropagateCompletion=true});
            var home = $"https://www.mzitu.com/{KEY}/";
            var dd = new HtmlDocument();
            dd.Load(client.GetStreamAsync(home).Result);
            var pages = dd.DocumentNode.SelectNodes("//div[@class='nav-links']/a");
            var length = Int16.Parse(pages[pages.Count - 2].InnerText);
            f.Post(home);
            for (int i = 2; i <= length; i++) 
            {
                f.Post($"{home}page/{i}/");
            }
            
            f.Complete();
            d.Completion.Wait();
            client.Dispose();
        }
    }
}
