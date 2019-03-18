using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GDriveFiles.Classes;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Json;
using Google.Apis.Logging;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;

namespace GDriveFiles
{
    class Program
    {
        static void Main(string[] args)
        {
            var name = ParseFolder(args);

            Authorize();

            var request = _driveService.Files.List();
            request.PageSize = 10;
            request.Q = $"name='{name}'";
            request.Fields = $"files({FileNode.FileFields})";

            var response = request.Execute();
            var root = response.Files.First();

            var node = new FileNode(_driveService, root);

            Task.WaitAll(node.Search());

            // Получили всю карту папки 
            var rows = node.GetRows();

            CreateAndWriteSheet(rows);
        }

        static void CreateAndWriteSheet(List<List<string>> rows)
        {
            var name = $"Google Drive Map on {DateTime.Now:dd.MM.yyyy hh:mm:ss}";

            var spreadSheet = new Spreadsheet
            {
                Properties = new SpreadsheetProperties
                {
                    Title = name,
                },

                Sheets = new List<Sheet>
                {
                    new Sheet
                    {
                        Properties = new SheetProperties
                        {
                            Title = "Main",
                        },
                    }
                },
            };

            var sheetRequest = _sheetService.Spreadsheets.Create(spreadSheet);
            sheetRequest.Fields = _sheetsFields;
            spreadSheet = sheetRequest.Execute();

            var values = new ValueRange
            {
                Values = rows.Select(x => (IList<object>)x.Cast<object>().ToList()).ToList(),
                MajorDimension = "ROWS",
                Range = "Main",
            };

            var updReq = _sheetService.Spreadsheets.Values.Update(values, spreadSheet.SpreadsheetId,
                values.Range);
            updReq.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            updReq.Fields = _sheetsFields;
            var updResponse = updReq.Execute();

            var createdSpredsheetRequest = _sheetService.Spreadsheets.Get(spreadSheet.SpreadsheetId);
            createdSpredsheetRequest.Fields = _sheetsFields;
            var createdSpredsheet = createdSpredsheetRequest.Execute();

            Console.WriteLine($"Created spreadsheet - {name}");
        }

        static string ParseFolder(string[] args)
        {
            // Сначала проверяем агрументы
            if (args.Any() && !string.IsNullOrEmpty(args[0]))
            {
                return args[0];
            }

            Console.WriteLine("Введите точное название папки для составления её карты");
            var name = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(name))
                throw new Exception("Введите правильное имя папки");

            return name;
        }

        private static DriveService _driveService;
        private static SheetsService _sheetService;
        private static string _sheetsFields = "spreadsheetId";

        private static void Authorize()
        {
            var credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(new MemoryStream(Properties.Resources.workingCredentials)).Secrets,
                    new[] 
                    {
                        DriveService.Scope.DriveMetadataReadonly,
                        SheetsService.Scope.Spreadsheets
                    },
                    "user",
                    CancellationToken.None,
                    new FileDataStore("tokens", true))
                .Result;

            var initializer = new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = Assembly.GetExecutingAssembly().GetName().Name
            };

            _driveService = new DriveService(initializer);
            _sheetService = new SheetsService(initializer);
        }
    }
}
