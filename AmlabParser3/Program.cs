using System;

namespace AmlabParser3
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.Write("Путь до файла скачиваний: ");
            string pathToDownloadsList = Console.ReadLine();

            Console.Write("Путь сохранения файлов: ");
            string outDirectory = Console.ReadLine();

            Console.Write("Токен аутентификации: ");
            string authToken = Console.ReadLine();

            Console.Write("Степень параллельности: ");
            int maxDegreeOfParallelism = int.Parse(Console.ReadLine());

            AmlabDownloader downloader = new AmlabDownloader();
            downloader.DownloadList(pathToDownloadsList, outDirectory, authToken, maxDegreeOfParallelism);
        }
    }
}