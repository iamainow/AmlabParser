using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AmlabParser3
{
    public class AmlabDownloader
    {
        private string replaceInvalidFileChars(string filename)
        {
            string result = filename;
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalidChar.ToString(), "");
            }
            return result;
        }
        private string getTextBetween(string text, string startText, string endText)
        {
            int startIndex = text.IndexOf(startText, StringComparison.OrdinalIgnoreCase) + startText.Length;
            int endIndex = text.IndexOf(endText, startIndex, StringComparison.OrdinalIgnoreCase);
            int length = endIndex - startIndex;

            return text.Substring(startIndex, length);
        }
        private string getReplacingValues(string text, string startText)
        {
            int startIndex = text.LastIndexOf(startText, StringComparison.OrdinalIgnoreCase) + startText.Length;
            return text.Substring(startIndex);
        }
        public void DownloadUrl(string courseTag, string outDirectory, string authToken)
        {
            RestClient client = new RestClient();

            string courseDirectory = Path.Combine(outDirectory, courseTag);
            if (!Directory.Exists(courseDirectory))
            {
                Directory.CreateDirectory(courseDirectory);
            }

            IRestRequest lessonsRequest = new RestRequest($"https://beta.amlab.me/{courseTag}/study/lessons/", Method.GET, DataFormat.None);
            lessonsRequest.AddCookie("AUTH_TOKEN", authToken);
            lessonsRequest.AddHeader("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.88 Safari/537.36");
            IRestResponse lessonsResponse = client.Execute(lessonsRequest);

            IRestRequest descriptionRequest = new RestRequest($"https://amlab.me/api/landings/enrollment/{courseTag}/", Method.GET, DataFormat.Json);
            IRestResponse descriptionResponse = client.Execute(descriptionRequest);
            
            File.WriteAllText(Path.Combine(courseDirectory, "landing.json"), descriptionResponse.Content);

            string script = this.getTextBetween(lessonsResponse.Content, @"<script>window.__NUXT__=(function", @"));</script>");
            string[] replacingVars = this.getTextBetween(script, "(", ")").Split(',');
            string[] replacingValues = JsonConvert.DeserializeObject<string[]>("[" + this.getReplacingValues(script, "(") + "]");

            if (replacingVars.Length != replacingValues.Length)
            {
                ConsoleHelper.Default.WriteErrorLines($"найдено {replacingVars.Length} аргументов и {replacingValues.Length} значений");
                return;
            }

            var replacings = Enumerable.Zip(replacingVars, replacingValues, (var, value) => (var, value: value?.Replace("\"", "\\\"")));

            string downloadListString = this.getTextBetween(lessonsResponse.Content, @"data:[{", @"}],error:");
            downloadListString = "[{" + downloadListString + "}]";

            foreach (var replacing in replacings)
            {
                downloadListString = downloadListString.Replace(":" + replacing.var, ": \"" + replacing.value + "\"");
            }

            File.WriteAllText(Path.Combine(courseDirectory, "lessons.json"), downloadListString);

            Data[] datas = JsonConvert.DeserializeObject<Data[]>(downloadListString);

            Playlist downloadList = datas.Single().playlist;
            string courseName = downloadList.name;

            int index = 1;
            foreach (Collection collection in downloadList.collections)
            {
                string chapter = $"{(index++).ToString("D2")} {collection.name}";
                foreach (Media media in collection.medias)
                {
                    string filename = this.replaceInvalidFileChars(media.video_name + ".mp4");
                    string directory = Path.Combine(outDirectory, this.replaceInvalidFileChars(courseTag), this.replaceInvalidFileChars(chapter));
                    string path = Path.Combine(directory, filename);

                    IRestRequest qualityRequest = new RestRequest($"https://embed.new.video/{media.integros_id}.js?sig=", Method.GET, DataFormat.None);
                    IRestResponse qualityResponse = client.Execute(qualityRequest);

                    string qualitiesString = this.getTextBetween(qualityResponse.Content, @"""use strict"";", @"var playerOptions");
                    qualitiesString = this.getTextBetween(qualitiesString, "{", "}");
                    qualitiesString = "{" + $"{qualitiesString}" + "}";

                    Dictionary<string, string> qualities = JsonConvert.DeserializeObject<Dictionary<string, string>>(qualitiesString);
                    string maxQuality = qualities.Max(A => int.Parse(A.Key)).ToString();

                    string videourl = qualities[maxQuality];

                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    IRestRequest videoOptionsRequest = new RestRequest(videourl, Method.HEAD, DataFormat.None);
                    IRestResponse videoOptions = client.Execute(videoOptionsRequest);
                    long fileSizeFromUrl = videoOptions.ContentLength;

                    if (File.Exists(path))
                    {
                        long fileSizeFromDisk = new FileInfo(path).Length;
                        if (fileSizeFromUrl == 0)
                        {
                            ConsoleHelper.Default.WriteErrorLines(true, $"ERROR: file {path}, reason: remote filesize == 0");
                        }
                        else if (fileSizeFromUrl == fileSizeFromDisk)
                        {
                            ConsoleHelper.Default.WriteInfoLines($"skip file {path}, reason: equals filesizes {fileSizeFromUrl}");
                            continue;
                        }
                        else if (fileSizeFromUrl > fileSizeFromDisk)
                        {
                            ConsoleHelper.Default.WriteInfoLines($"redownload file {path}, reason: remote size {fileSizeFromUrl} > local size {fileSizeFromDisk}");
                            File.Delete(path);
                        }
                        else if (fileSizeFromUrl < fileSizeFromDisk)
                        {
                            ConsoleHelper.Default.WriteErrorLines(true, $"ERROR: redownload file {path}, reason: remote size {fileSizeFromUrl} < local size {fileSizeFromDisk}");
                            File.Delete(path);
                        }
                    }

                    ConsoleHelper.Default.WriteInfoLines($"file saving {path} {fileSizeFromUrl >> 20}MiB");

                    IRestRequest videoRequest = new RestRequest(videourl, Method.GET, DataFormat.None);
                    videoRequest.ResponseWriter = responseStream =>
                    {
                        using (FileStream fileStream = new FileStream(path, FileMode.Create))
                        using (responseStream)
                        {
                            responseStream.CopyTo(fileStream);
                        }
                    };
                    client.Execute(videoRequest);

                    ConsoleHelper.Default.WriteInfoLines($"file saved {path} {fileSizeFromUrl >> 20}MiB");
                }
            }
        }
        public void DownloadList(string pathToDownloadsList, string outDirectory, string authToken, int maxDegreeOfParallelism)
        {
            Download[] downloads = JsonConvert.DeserializeObject<Download[]>(File.ReadAllText(pathToDownloadsList));
            Parallel.ForEach(downloads, new ParallelOptions() { MaxDegreeOfParallelism = maxDegreeOfParallelism }, download =>
            {
                try
                {
                    string path = Path.Combine(outDirectory, download.directory);
                    ConsoleHelper.Default.WriteInfoLines($"course processing {download.urlTag} to directory {path}");
                    this.DownloadUrl(download.urlTag, path, authToken);
                    ConsoleHelper.Default.WriteInfoLines($"course processed {download.urlTag} to directory {path}");
                }
                catch (Exception exception)
                {
                    ConsoleHelper.Default.WriteErrorLines(true, exception.ToString());
                }
            });
        }
    }
}