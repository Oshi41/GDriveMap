using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Sheets.v4.Data;

namespace GDriveFiles.Classes
{
    class FileNode
    {
        private readonly DriveService _service;

        private readonly string _folderType = "application/vnd.google-apps.folder";

        public FileNode(DriveService service, File file)
        {
            _service = service;
            File = file;
        }

        public async Task Search()
        {
            if (!_folderType.Equals(File.MimeType))
                return;

            string token = null;

            do
            {

                var request = _service.Files.List();
                //API max
                request.PageSize = 1000;
                request.Q = $"'{File.Id}' in parents";
                request.Fields = $"nextPageToken, files({FileFields})";
                request.PageToken = token;

                var result = await request.ExecuteAsync();

                if (result.Files.Any())
                {
                    Children.AddRange(result.Files.Select(x => new FileNode(_service, x)));
                }

                token = result.NextPageToken;

            } while (token != null);

            Console.WriteLine($"{File.Name} - {Children.Count} files/folders");

            var tasks = Children.Select(x => x.Search());
            await Task.WhenAll(tasks);

            
        }

        public List<List<string>> GetRows(uint depth = 0)
        {
            var result = new List<List<string>>();

            // В каждой строке 2 столбца - Имя и ссылка на просмотр
            var row = new List<string>
            {
                File.Name,
                File.WebViewLink
            };

            // каждая следующая ячейка должна быть с отступом
            for (int i = 0; i < depth; i++)
            {
                row.Insert(0, string.Empty);
            }

            result.Add(row);

            // Сначала показываю папки
            foreach (var child in Children.Where(x => x.Children.Any()))
            {
                result.AddRange(child.GetRows(depth + 1));
            }

            // Потом идут файлы
            foreach (var child in Children.Where(x => !x.Children.Any()))
            {
                result.AddRange(child.GetRows(depth + 1));
            }

            return result;
        }

        public File File { get; }
        public List<FileNode> Children { get; } = new List<FileNode>();

        public static string FileFields { get; } = "id, name, webViewLink, parents, mimeType";
    }
}
