using MimeKit;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZurichInsuranceTool
{
    class Program
    {
        public static string pathFileName = "setting.json";
        public static string currentDirectory = Directory.GetCurrentDirectory();
        public static string pathFileSetting = Path.Combine(currentDirectory, pathFileName);
        public static string desDirectoryInsurance = currentDirectory + @"\tmp\Insurance";
        public static string desDirectoryReservation = currentDirectory + @"\tmp\Reservation";
        public static string year = DateTime.Now.AddYears(0).Year.ToString();
        public static string month = DateTime.Now.AddMonths(-1).ToString("MM");
        public static double totalInsurance20427 = 0;
        public static double totalInsurance20428 = 0;
        public static double totalInsurance = 0;
        public static double totalReservationPeople = 0;
        public static string logFilePath = null;
        static void Main(string[] args)
        {
            string logFolder = currentDirectory + @"\log";
            if (!Directory.Exists(logFolder))
            {
                // Create forder
                Directory.CreateDirectory(logFolder);
            }
            string logFileName = "loger-" + DateTime.Now.ToString("yyyyMM") + ".log";
            logFilePath = Path.Combine(logFolder, logFileName);
            if (!Directory.Exists(pathFileSetting))
            {
                LogToFile(logFilePath, "Setting file not exit");
                Thread.Sleep(1000);
            }
            string jsonContent = File.ReadAllText(pathFileSetting, Encoding.UTF8);
            //conver json to Object
            JsonSettingObject jsonObject = JsonConvert.DeserializeObject<JsonSettingObject>(jsonContent);
            bool checkNormalSetting = jsonObject.NormalSettings.CopyFileFromServer;
            if (checkNormalSetting)
            {
                if (NormalCopyLog())
                {
                    if (jsonObject.NormalSettings.SearchReservationContent)
                    {
                        NormalSearchContent();
                    }
                }
            }
            else
            {
                if (CopyFileInsuranceFromServer() && CopyFileReservationFromServer())
                {
                    if (jsonObject.InsuranceFileSettings.SearchInsuranceContent)
                    {
                        Dictionary<string, string> reservationList = ReadFileInsurance();
                        if (reservationList.Count > 0)
                        {
                            if (ReadFileReservation(reservationList))
                            {
                                if (CalculatorReservationNumberPeople())
                                {
                                    if (jsonObject.Mail.Send_flg)
                                    {
                                        var body = "お疲れ様です。\r\n";
                                        body += $"以下のチューリッヒ保険集計{month}月分です。\r\n ";
                                        body += "自動メールなのでご返事不要となります。\r\n よろしくお願いいたします。\r\n\r\n";
                                        //保険販売対象者数（＝乗車券枚数）  : xxxx人（＝xxxx枚）
                                        //証券枚数 20427                    : xxxx枚
                                        //加入率                            : xx %
                                        //証券枚数購入失敗                  : xxxx枚

                                        body += $"保険販売対象者数（＝乗車券枚数）  : {totalReservationPeople}人（＝{totalReservationPeople}枚）\r\n";
                                        body += $"証券枚数 20427  : {totalInsurance20427}枚\r\n";
                                        //body += $"証券枚数(TTL:)   : {totalInsurance}枚\r\n";
                                        double rate = (totalInsurance20427 / totalReservationPeople) * 100;
                                        body += $"加入率   : {Math.Round(rate, 2)}% \r\n";
                                        body += $"証券枚数購入失敗   : {totalInsurance20428}枚\r\n";


                                        body += "\r\n\r\n";

                                        var emailFromName = $"{jsonObject.Mail.Subject}{month}月分";
                                        var emailFromAddress = jsonObject.Mail.From;
                                        var emailTo = jsonObject.Mail.To;
                                        var emailBcc = jsonObject.Mail.Bcc;
                                        var subject = $"{jsonObject.Mail.Subject}{month}月分";
                                        var mailMessage = body;
                                        var smtpUser = jsonObject.Mail.Uid;
                                        var smtpPwd = jsonObject.Mail.Pwd;
                                        var smtpServer = jsonObject.Mail.SmtpServer;
                                        var smtpPort = jsonObject.Mail.Port;
                                        SendEmail(emailFromName, emailFromAddress, emailTo, emailBcc, subject, mailMessage, smtpUser, smtpPwd, smtpServer, smtpPort);
                                        if (jsonObject.NormalSettings.DeleteAllLogFilesWhenProcessingIsSuccessful)
                                        {
                                            LogToFile(logFilePath, "Delete all file start");
                                            Console.WriteLine("Delete all file start");
                                            DeleteAllFile(desDirectoryInsurance);
                                            DeleteAllFile(desDirectoryReservation);
                                            LogToFile(logFilePath, "Delete all file end");
                                            Console.WriteLine("Delete all file end");
                                        }

                                    }
                                }
                            }
                        }
                    }
                }
            }


            Thread.Sleep(1000);

        }
        public static Dictionary<string, string> ReadFileInsurance()
        {
            string searchPattern20427 = @".*Policy Success.*";
            string searchPattern20428 = @".*Quote Error.*";
            string searchPatternResNumber = @"(\d{4}-\d{2}-\d{2}).*BusReservationNumber=(\d+)";
            string destemp = currentDirectory + @"\tmp\Insurance\temp";
            string jsonContent = File.ReadAllText(pathFileSetting);
            //conver json to Object
            JsonSettingObject jsonObject = JsonConvert.DeserializeObject<JsonSettingObject>(jsonContent);
            if (!jsonObject.InsuranceFileSettings.SearchInsuranceDefaultSearchPattern)
            {
                searchPattern20427 = jsonObject.InsuranceFileSettings.SearchPattern20427;
                searchPattern20428 = jsonObject.InsuranceFileSettings.SearchPattern20428;
                searchPatternResNumber = jsonObject.InsuranceFileSettings.SearchPatternResNumber;
            }

            LogToFile(logFilePath, "Read File Insurance Start");
            try
            {
                // check exis folder
                if (!Directory.Exists(destemp))
                {
                    // Create forder
                    Directory.CreateDirectory(destemp);
                }
                string[] files = Directory.GetFiles(desDirectoryInsurance);
                using (StreamWriter writer = new StreamWriter(destemp + @"\" + year + month + "_20427.log", false))
                {
                    foreach (string filePath in files)
                    {
                        string fileContent = File.ReadAllText(filePath);
                        MatchCollection matches = Regex.Matches(fileContent, searchPattern20427);
                        foreach (Match match in matches)
                        {
                            Console.WriteLine("Result :" + match.Value);
                            LogToFile(logFilePath, match.Value);
                            totalInsurance20427++;
                            writer.Write(match.Value);
                        }
                    }
                }
                using (StreamWriter writer = new StreamWriter(destemp + @"\" + year + month + "_20428.log", false))
                {
                    foreach (string filePath in files)
                    {
                        string fileContent = File.ReadAllText(filePath);
                        MatchCollection matches = Regex.Matches(fileContent, searchPattern20428);

                        foreach (Match match in matches)
                        {
                            Console.WriteLine("Result :" + match.Value);
                            LogToFile(logFilePath, match.Value);
                            totalInsurance20428++;
                            writer.Write(match.Value);
                        }
                    }
                }
                Console.WriteLine("Result 20427 buy Success :" + totalInsurance20427);
                Console.WriteLine("Result 20428 buy Success :" + totalInsurance20428);
                totalInsurance = totalInsurance20427 + totalInsurance20428;
                Console.WriteLine("Result totalInsurance :" + totalInsurance);
                LogToFile(logFilePath, "Result 20427 buy Success :" + totalInsurance20427);
                LogToFile(logFilePath, "Result 20428 buy Success :" + totalInsurance20428);
                LogToFile(logFilePath, "Result totalInsurance :" + totalInsurance);

                string[] resultInsuranceFiles = Directory.GetFiles(destemp);
                Dictionary<string, string> uniqueNumbers = new Dictionary<string, string>();
                foreach (string filePath in resultInsuranceFiles)
                {
                    string[] fileContent = File.ReadAllLines(filePath);
                    foreach (string line in fileContent)
                    {
                        Match match = Regex.Match(line, searchPatternResNumber);
                        if (match.Success)
                        {
                            string date = match.Groups[1].Value; // Date reservation
                            string busReservationNumber = match.Groups[2].Value;
                            if (!uniqueNumbers.ContainsKey(busReservationNumber))
                            {
                                uniqueNumbers.Add(busReservationNumber, date);
                                Console.WriteLine("Bus Reservation Number uniqueNumbers:" + busReservationNumber + ", date: " + date);
                                LogToFile(logFilePath, "Bus Reservation Number uniqueNumbers:" + busReservationNumber + ", date: " + date);
                                Console.WriteLine("Bus Reservation Number: " + busReservationNumber + ", date :" + date);
                            }

                        }

                    }
                }
                LogToFile(logFilePath, "Read File Insurance End");
                return uniqueNumbers;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error :" + ex.Message);
                LogToFile(logFilePath, "Error" + ex.Message);
                return new Dictionary<string, string>();
            }

        }

        public static bool ReadFileReservation(Dictionary<string, string> resvervationNumberList)
        {
            string destemp = currentDirectory + @"\tmp\Reservation\temp";
            // read conten file
            string jsonContent = File.ReadAllText(pathFileSetting);

            //conver json to Object
            JsonSettingObject jsonObject = JsonConvert.DeserializeObject<JsonSettingObject>(jsonContent);
            // check exis folder
            if (!Directory.Exists(destemp))
            {
                // Create forder
                Directory.CreateDirectory(destemp);
            }
            LogToFile(logFilePath, "Read File Reservation Start");
            try
            {
                using (StreamWriter writer = new StreamWriter(destemp + @"\" + year + month + "Reservation.log", false))
                {
                    foreach (var item in resvervationNumberList)
                    {
                        var fileDate = item.Value.Replace("-", "");
                        var date = item.Key.Substring(0, 8);
                        var resNumber = item.Key.Substring(8);
                        //".*""ukhz"":""20230618"",""zuno"":""07000111"",.*""Payment_Status"":""1"""
                        //"(?:.)*?""ukhz"":""20230701"",""zuno"":""08024602""(?:.)*?""Payment_Status"":""1"""g
                        string searchPattern = @"""ukhz"":""" + date + @""",""zuno"":""" + resNumber + @"""(?:.)*?""Payment_Status"":""1""";
                        string searchPatternFileName = @"ReservationRepository-.*-.?" + fileDate + ".log";
                        if (!jsonObject.ReservationFileSettings.SearchReservationDefaultSearchPattern)
                        {
                            searchPattern = jsonObject.ReservationFileSettings.SearchReservationContentPattern;
                            searchPatternFileName = jsonObject.ReservationFileSettings.FillterFileReservationContentPattern;
                        }
                        string[] files = Directory.GetFiles(desDirectoryReservation).Where(f => Regex.IsMatch(Path.GetFileName(f), searchPatternFileName))
                               .ToArray();
                        Console.WriteLine("Searching....................... " + DateTime.Now + " date:" + date + " ,resNumber:" + resNumber);
                        LogToFile(logFilePath, "Searching....................... " + DateTime.Now + " date:" + date + " ,resNumber:" + resNumber);
                        bool foundMatch = false;
                        foreach (string file in files)
                        {

                            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                            using (BufferedStream bs = new BufferedStream(fs))
                            using (StreamReader reader = new StreamReader(bs))
                            {
                                string line;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    Match match = Regex.Match(line, searchPattern);
                                    if (match.Success)
                                    {
                                        Console.WriteLine("Result :" + match);
                                        writer.WriteLine(match);
                                        LogToFile(logFilePath, match.ToString());
                                        foundMatch = true;
                                        break;
                                    }
                                    if (foundMatch)
                                    {
                                        break;
                                    }
                                }

                            }
                            if (foundMatch)
                            {
                                break;
                            }
                        }
                    }
                }
                LogToFile(logFilePath, "Read File Reservation End");
                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error :" + ex.Message);
                LogToFile(logFilePath, "Error :" + ex.Message);
                return false;
            }

        }

        public static bool CalculatorReservationNumberPeople()
        {
            string destemp = currentDirectory + @"\tmp\Reservation\temp";
            string searchPatternPeople = @"""otrk"":\[(\d+(?:,\d+)*)\],""onrk"":\[(\d+(?:,\d+)*)\]";
            LogToFile(logFilePath, "CalculatorReservationNumberPeople Start");
            try
            {
                string[] resultInsuranceFiles = Directory.GetFiles(destemp);
                foreach (string filePath in resultInsuranceFiles)
                {
                    string[] fileContent = File.ReadAllLines(filePath);
                    foreach (string line in fileContent)
                    {
                        Match match = Regex.Match(line, searchPatternPeople);
                        if (match.Success)
                        {
                            string otrkGroup = match.Groups[1].Value;
                            string onrkGroup = match.Groups[2].Value;
                            int[] otrkValues = Array.ConvertAll(otrkGroup.Split(','), int.Parse);
                            int[] onrkValues = Array.ConvertAll(onrkGroup.Split(','), int.Parse);
                            int otrkSum = otrkValues.Sum();
                            int onrkSum = onrkValues.Sum();
                            Console.WriteLine("otrk: " + string.Join(", ", otrkValues));
                            Console.WriteLine("onrk: " + string.Join(", ", onrkValues));
                            Console.WriteLine("People Total: " + (otrkSum + onrkSum));
                            LogToFile(logFilePath, "otrk: " + string.Join(", ", otrkValues));
                            LogToFile(logFilePath, "onrk: " + string.Join(", ", onrkValues));
                            LogToFile(logFilePath, "People Total: " + (otrkSum + onrkSum));
                            totalReservationPeople += (otrkSum + onrkSum);

                        }

                    }
                }
                Console.WriteLine("People Reservation Total: " + totalReservationPeople);
                LogToFile(logFilePath, "People Reservation Total: " + totalReservationPeople);
                LogToFile(logFilePath, "CalculatorReservationNumberPeople End");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error :" + ex.Message);
                LogToFile(logFilePath, "Error" + ex.Message);
                return false;
            }
        }
        public static bool CopyFileInsuranceFromServer()
        {
            //InsuranceService-JBUS-140-20230808.log
            string searchPattern = @"InsuranceService-.*-.?" + year + month + "\\d{2}.log";
            LogToFile(logFilePath, "Copy File Insurance Start");
            try
            {
                // check exis folder
                if (!Directory.Exists(desDirectoryInsurance))
                {
                    // Create forder
                    Directory.CreateDirectory(desDirectoryInsurance);
                }
                // read conten file
                string jsonContent = File.ReadAllText(pathFileSetting);

                //conver json to Object
                JsonSettingObject jsonObject = JsonConvert.DeserializeObject<JsonSettingObject>(jsonContent);
                //get Url Object
                string[] serverUrls = jsonObject.ServerUrl;

                if (!jsonObject.InsuranceFileSettings.SearchInsuranceDefaultSearchFilePattern)
                {
                    searchPattern = jsonObject.InsuranceFileSettings.SearchFilePatternInsurance;
                }

                foreach (string serverUrl in serverUrls)
                {
                    if (Directory.Exists(serverUrl))
                    {
                        string[] logFiles = Directory.GetFiles(serverUrl)
                               .Where(f => Regex.IsMatch(Path.GetFileName(f), searchPattern))
                               .ToArray();
                        Console.WriteLine("files:");

                        foreach (string logFile in logFiles)
                        {
                            Console.WriteLine(logFile);
                            string fileName = Path.GetFileName(logFile);
                            string destinationPath = Path.Combine(desDirectoryInsurance, fileName);

                            //int count = 1;
                            //while (File.Exists(destinationPath))
                            //{
                            //    string newFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{count}{Path.GetExtension(fileName)}";
                            //    destinationPath = Path.Combine(desDirectoryInsurance, newFileName);
                            //    count++;
                            //}
                            File.Copy(logFile, destinationPath, true);// true: overwrite existing file
                            Console.WriteLine($"Copy'{fileName}' success!!!");
                            LogToFile(logFilePath, $"Copy'{fileName}' success!!!");
                        }

                    }
                    else
                    {
                        Console.WriteLine("not exit folder or VPN not connecting...");
                        LogToFile(logFilePath, "not exit folder or VPN not connecting...");
                        return false;
                    }
                }
                LogToFile(logFilePath, "Copy File Insurance End");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                LogToFile(logFilePath, "Error: " + ex.Message);
                return false;
            }
        }

        public static bool CopyFileReservationFromServer()
        {
            string searchPattern = @"ReservationRepository-.*-.?" + year + month + "\\d{2}.log";
            LogToFile(logFilePath, "Copy File Reservation Start");
            try
            {
                // check exis folder
                if (!Directory.Exists(desDirectoryReservation))
                {
                    Directory.CreateDirectory(desDirectoryReservation);
                }
                // read conten file
                string jsonContent = File.ReadAllText(pathFileSetting);

                //conver json to Object
                JsonSettingObject jsonObject = JsonConvert.DeserializeObject<JsonSettingObject>(jsonContent);
                //get Url Object
                string[] serverUrls = jsonObject.ServerUrl;
                if (!jsonObject.InsuranceFileSettings.SearchInsuranceDefaultSearchFilePattern)
                {
                    searchPattern = jsonObject.InsuranceFileSettings.SearchFilePatternInsurance;
                }
                foreach (string serverUrl in serverUrls)
                {
                    if (Directory.Exists(serverUrl))
                    {
                        //string[] files = Directory.GetFiles(hobby);
                        string[] logFiles = Directory.GetFiles(serverUrl)
                               .Where(f => Regex.IsMatch(Path.GetFileName(f), searchPattern))
                               .ToArray();
                        Console.WriteLine("files:");
                        //Parallel.ForEach(logFiles, sourceFilePath =>
                        //{
                        //    string destFilePath = desDirectoryReservation;
                        //    File.Copy(sourceFilePath, destFilePath, true); // true: overwrite existing file
                        //});
                        foreach (string logFile in logFiles)
                        {
                            Console.WriteLine(logFile);
                            string fileName = Path.GetFileName(logFile); // Lấy tên tệp từ đường dẫn đầy đủ
                            string destinationPath = Path.Combine(desDirectoryReservation, fileName);
                            File.Copy(logFile, destinationPath, true);
                            Console.WriteLine($"Copy'{fileName}' success!!!");
                            LogToFile(logFilePath, $"Copy'{fileName}' success!!!");
                        }
                    }
                    else
                    {

                        Console.WriteLine("not exit folder or VPN not connecting...");
                        LogToFile(logFilePath, "not exit folder or VPN not connecting...");
                        return false;
                    }

                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                LogToFile(logFilePath, "Error: " + ex.Message);
                return false;
            }
        }

        public static bool NormalCopyLog()
        {
            string desDirectoryNormal = currentDirectory + @"\tmp\normal";
            LogToFile(logFilePath, "Copy File Normal Start");
            try
            {
                // check exis folder
                if (!Directory.Exists(desDirectoryNormal))
                {
                    Directory.CreateDirectory(desDirectoryNormal);
                }
                // read conten file
                string jsonContent = File.ReadAllText(pathFileSetting);

                //conver json to Object
                JsonSettingObject jsonObject = JsonConvert.DeserializeObject<JsonSettingObject>(jsonContent);

                string SearchFilePattern = jsonObject.NormalSettings.SearchFilePattern;

                //get Url Object
                string[] serverUrls = jsonObject.ServerUrl;
                foreach (string serverUrl in serverUrls)
                {
                    if (Directory.Exists(serverUrl))
                    {
                        string[] logFiles = Directory.GetFiles(serverUrl)
                               .Where(f => Regex.IsMatch(Path.GetFileName(f), SearchFilePattern))
                               .ToArray();
                        Console.WriteLine("files:");
                        foreach (string logFile in logFiles)
                        {
                            Console.WriteLine(logFile);
                            string fileName = Path.GetFileName(logFile);
                            string desPath = Path.Combine(desDirectoryNormal, fileName);
                            File.Copy(logFile, desPath, true);
                            Console.WriteLine($"Copy'{fileName}' success!!!");
                            LogToFile(logFilePath, $"Copy'{fileName}' success!!!");
                        }
                    }
                    else
                    {

                        Console.WriteLine("not exit folder or VPN not connecting...");
                        LogToFile(logFilePath, "not exit folder or VPN not connecting...");
                        return false;
                    }

                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                LogToFile(logFilePath, "Error: " + ex.Message);
                return false;
            }
        }

        public static bool NormalSearchContent()
        {
            string desDirectoryNormal = currentDirectory + @"\tmp\normal";

            LogToFile(logFilePath, "Read File Reservation Start");
            try
            {
                // read conten file
                string jsonContent = File.ReadAllText(pathFileSetting);

                //conver json to Object
                JsonSettingObject jsonObject = JsonConvert.DeserializeObject<JsonSettingObject>(jsonContent);
                string searchReservationContentPattern = jsonObject.NormalSettings.SearchReservationContentPattern;

                using (StreamWriter writer = new StreamWriter(desDirectoryNormal + @"\" + year + month + "Normal.log", false))
                {
                    string[] files = Directory.GetFiles(desDirectoryNormal);
                    Console.WriteLine("Searching....................... " + DateTime.Now);
                    LogToFile(logFilePath, "Searching....................... " + DateTime.Now);
                    foreach (string file in files)
                    {
                        string fileContent = File.ReadAllText(file);
                        MatchCollection matches = Regex.Matches(fileContent, searchReservationContentPattern);
                        foreach (Match match in matches)
                        {
                            if (match.Success)
                            {
                                Console.WriteLine("Result :" + match);
                                writer.Write(match);
                                LogToFile(logFilePath, match.ToString());
                            }
                        }
                    }
                }
                LogToFile(logFilePath, "Read File Reservation End");
                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error :" + ex.Message);
                LogToFile(logFilePath, "Error :" + ex.Message);
                return false;
            }
        }

        public static void LogToFile(string filePath, string logMessage)
        {
            using (StreamWriter writer = new StreamWriter(filePath, true))
            {
                writer.WriteLine(DateTime.Now + "[Log] " + logMessage);
            }
        }

        public static void SendEmail(string emailFromName, string emailFromAddress, string emailTo, string emailcc, string subject, string message, string smtpUser, string smtpPwd, string smtpServer, int sendPort)
        {
            var msg = new MimeMessage();

            // 送信者を用意する
            var from = new MailboxAddress(emailFromName, emailFromAddress);

            // 送信者の情報を設定（追加）する
            msg.From.Add(from);

            // 宛先を用意する
            var to = new MailboxAddress(string.Empty, emailTo);

            // 宛先の情報を設定（追加）する
            msg.To.Add(to);

            // Ccを用意する
            var cc = new MailboxAddress(string.Empty, emailcc);

            // Ccの情報を設定（追加）する
            msg.Cc.Add(cc);

            // 件名を設定する
            msg.Subject = subject;

            // 本文を設定する
            // 本文のテキストのフォーマットを設定します。
            msg.Body = new TextPart("plain")
            {
                Text = message
            };

            // メールの送信
            using (var sc = new MailKit.Net.Smtp.SmtpClient())
            {
                try
                {
                    //sc.Timeout = 1000 * 2;
                    // SMTPサーバに接続する
                    sc.Connect(smtpServer, sendPort, false);

                    // SMTP認証
                    sc.Authenticate(smtpUser, smtpPwd);

                    // メールを送信する
                    sc.Send(msg);

                    // SMTPサーバを切断する
                    sc.Disconnect(true);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

        }

        public static void DeleteAllFile(string folderPath)
        {
            //get file in folder
            string[] files = Directory.GetFiles(folderPath);
            Parallel.ForEach(files, (file) =>
            {
                File.Delete(file);
                Console.WriteLine("Deleted file: " + file);
            });
            Console.WriteLine("All files deleted successfully.");
        }
    }



    public class JsonSettingObject
    {
        public string[] ServerUrl { get; set; }
        public InsuranceFileSettings InsuranceFileSettings;
        public ReservationFileSettings ReservationFileSettings;
        public NormalSettings NormalSettings;
        public Mail Mail;

    }
    public class InsuranceFileSettings
    {
        public bool SearchInsuranceDefaultSearchFilePattern { get; set; }
        public string SearchFilePatternInsurance { get; set; }
        public bool SearchInsuranceDefaultSearchPattern { get; set; }
        public bool SearchInsuranceContent { get; set; }
        public string SearchPattern20427 { get; set; }
        public string SearchPattern20428 { get; set; }
        public string SearchPatternResNumber { get; set; }
    }

    public class ReservationFileSettings
    {
        public string SearchReservationDefaultSearchFilePattern { get; set; }
        public string SearchFilePatternReservation { get; set; }
        public bool SearchReservationDefaultSearchPattern { get; set; }
        public bool SearchReservationContent { get; set; }
        public string SearchReservationContentPattern { get; set; }
        public string FillterFileReservationContentPattern { get; set; }
    }

    public class NormalSettings
    {
        public bool CopyFileFromServer { get; set; }
        public string SearchFilePattern { get; set; }
        public bool SearchReservationContent { get; set; }
        public string SearchReservationContentPattern { get; set; }
        public bool DeleteAllLogFilesWhenProcessingIsSuccessful { get; set; }
    }

    public class Mail
    {
        public bool Send_flg { get; set; }
        public string SmtpServer { get; set; }
        public string Uid { get; set; }
        public string Pwd { get; set; }
        public string To { get; set; }
        public string Bcc { get; set; }
        public string From { get; set; }
        public string Replyto { get; set; }
        public int Port { get; set; }
        public string Subject { get; set; }
    }

}