using System;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;
using System.Net.Http;

namespace Bovespa2QuantConnect
{
    class Program
    {
        static readonly string _leanequityfolder = @"C:\Users\Alexandre\Documents\GitHub\Lean\Data\equity\";

        static void Main(string[] args)
        {
            char key;

            var menu =
                "1. Extract raw data from COTAHIST file to QC daily cvs file.\n" +
                "2. Extract raw data from NEG* files to QC tick cvs file.\n" +
                "3. Make QC minute cvs files from QC tick cvs file.\n" +
                //"4. NotImplemented.\n" +
                //"5. NotImplemented.\n" +
                //"6. NotImplemented.\n" +
                //"7. NotImplemented.\n" +
                //"8. NotImplemented.\n" +
                "9. Zip all raw data\n" +
                "0. Sair\n" +
                ">> Insira op��o: ";
            Console.Write(menu);

            do
            {
                key = Console.ReadKey().KeyChar;
                var dir = @"C:\Users\Alexandre\Documents\IBOV\Stock\";

                switch (key)
                {
                    case '1':
                        ReadZipFiles(new DirectoryInfo(dir).GetFiles("COTAHIST_A2*zip"), false);
                        break;
                    case '2':
                        ReadZipFiles(new DirectoryInfo(dir).GetFiles("NEG_20*zip"), false);
                        break;
                    case '3':
                        MakeMinuteFilesFromTickFiles();
                        break;
                    case '4':
                        //AdjustedPrice();
                        //COTAHIST2ASCII();                        
                        break;
                    case '5':
                        //GetLastFromCOTAHIST();
                        //COTAHIST2CSV();
                        break;
                    case '6':
                        //new string[] 
                        //{ 
                        //    //"ITUB4", "GGBR4", "CYRE3", "KROT3", "HGTX3", "MRFG3",  
                        //    "BBAS3"//,"JBSS3",  "POMO4", "USIM5", "ALLL3",
                        //}.ToList().ForEach(s => AdjustedPrice(s, new DateTime(2014, 5, 31)));
                        break;
                    case '7':
                        //ReadNEG(true, cutoff);
                        break;
                    case '8':
                        ReadZipFiles(new DirectoryInfo(dir).GetFiles("COTAHIST_A2*zip"), true);
                        break;
                    case '9':
                        ZipALLRaw();
                        break;
                    default:
                        Console.WriteLine("\nOp��o Inv�lida!\n" + menu);
                        break;
                }

            } while (key != '0');
        }

        private static async Task ReadZipFiles(FileInfo[] zipfiles, bool iswriteholidayfiles)
        {
            foreach (var zipfile in zipfiles)
            {
                using (var zip2open = new FileStream(zipfile.FullName, FileMode.Open, FileAccess.Read))
                {
                    using (var archive = new ZipArchive(zip2open, ZipArchiveMode.Read))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (iswriteholidayfiles)
                            {
                                await WriteQuantConnectHolidayFile(entry);
                            }
                            else
                            {
                                using (var file = new StreamReader(entry.Open()))
                                {
                                    while (!file.EndOfStream)
                                    {
                                        var line = await file.ReadLineAsync();

                                        if (zipfile.Name.Contains("NEG"))
                                            await WriteQuantConnectTickFile(line);
                                        else
                                            await WriteQuantConnectDailyFile(line);
                                    }
                                }
                            }
                        }
                    }
                }
                Console.WriteLine("> " + zipfile);
            }
        }

        private static async Task WriteQuantConnectHolidayFile(ZipArchiveEntry entry)
        {
            // America/Sao_Paulo,brazil,,equity,-,-,-,-,9.5,10,16.9166667,18,9.5,10,16.9166667,18,9.5,10,16.9166667,18,9.5,10,16.9166667,18,9.5,10,16.9166667,18,-,-,-,-
            var year = int.Parse(entry.Name.Substring(10, 4));
            var filedate = new DateTime(year + 1, 1, 1).AddDays(-1);
            var lastdate = new DateTime(1999, 1, 1);
            var outputfile = _leanequityfolder.Replace("equity", "market-hours") + "holidays-brazil.csv";
            var fileexists = File.Exists(outputfile) && 
                DateTime.TryParseExact(File.ReadAllLines(outputfile).ToList().Last(), "yyyy, MM, dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out lastdate);

            if (lastdate >= filedate) return;
            if (!fileexists) File.WriteAllText(outputfile, "year, month, day\r\n# Brazil Equity market holidays\r\n");
            
            var holidays = new List<DateTime>();

            using (var file = new StreamReader(entry.Open()))
            {
                filedate = new DateTime(year, 1, 1);
            
                while (filedate.Year == year)
                {
                    if (filedate.DayOfWeek != DayOfWeek.Saturday && filedate.DayOfWeek != DayOfWeek.Sunday) holidays.Add(filedate);
                    filedate = filedate.AddDays(1);
                }

                while (!file.EndOfStream)
                {
                    var line = await file.ReadLineAsync();
                    if (DateTime.TryParseExact(line.Substring(2, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out filedate))
                        holidays.Remove(filedate);
                }

                holidays.RemoveAll(d => d <= lastdate);
            }

            if (holidays.Last().Year == DateTime.Now.Year - 1)
            {
                var ids = new List<int>();
                var url = "http://www.bmfbovespa.com.br/pt-br/regulacao/calendario-do-mercado/calendario-do-mercado.aspx";
                var months = new string[] { "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez" }.ToList();

                try
                {
                    using (var client = new HttpClient())
                    using (var response = await client.GetAsync(url))
                    using (var content = response.Content)
                    {
                        var i = 0;
                        var id = 0;
                        var page = await content.ReadAsStringAsync();
                        page = page.Substring(0, page.IndexOf("linhaDivMais"));

                        while (i < 12)
                        {
                            while (i < 12 && (id = page.IndexOf(">" + months[i + 0] + "<")) < 0) i++;
                            var start = id + 1;

                            while (i < 11 && (id = page.IndexOf(">" + months[i + 1] + "<")) < 0) i++;
                            var count = id - start;

                            months[i] = count > 0 ? page.Substring(start, count) : page.Substring(start);

                            id = 0;
                            while ((id = months[i].IndexOf("img/ic_", id) + 6) > 6)
                            {
                                id++;
                                if (DateTime.TryParseExact(months[i].Substring(id, 2) + months[i].Substring(0, 3) + DateTime.Now.Year.ToString(),
                                    "ddMMMyyyy", CultureInfo.CreateSpecificCulture("pt-BR"), DateTimeStyles.None, out filedate))
                                    holidays.Add(filedate);                
                            }
                            
                            i++;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

                holidays.ForEach(d => File.AppendAllText(outputfile, d.Year.ToString("0000") + ", " + d.Month.ToString("00") + ", " + d.Day.ToString("00\r\n")));                
            }
        }

        private static async Task WriteQuantConnectTickFile(string line)
        {
            int type;
            if (!int.TryParse(line.Substring(15, 3), out type) || type > 11) return;

            line = line
                .Replace("BMEF", "BVMF")
                .Replace("TLPP", "VIVT")
                .Replace("VNET", "CIEL")
                .Replace("VCPA", "FIBR")
                .Replace("PRGA", "BRFS")
                .Replace("AMBV4", "ABEV3")
                .Replace("DURA4", "DTEX3");

            var data = line.Split(';');

            var symbol = data[1].Trim().ToLower();
            if (symbol != "bbas3") return;

            var dir = Directory.CreateDirectory(_leanequityfolder + @"tick\" + symbol + @"\");

            var csvfile = dir.FullName + data[0].Replace("-", "") + "_" + symbol + "_Trade_Tick.csv";

            var output = TimeSpan.Parse(data[5]).TotalMilliseconds.ToString() + ",";
            output += (Decimal.Parse(data[3].Replace(".", ",")) * 10000m).ToString("#.") + ",";
            output += Int64.Parse(data[4]) + Environment.NewLine;

            File.AppendAllText(csvfile, output);
        }

        private static async Task WriteQuantConnectDailyFile(string line)
        {
            int type;
            if (!int.TryParse(line.Substring(16, 3), out type) || type > 11) return;

            line = line
                .Replace("BMEF", "BVMF")
                .Replace("TLPP", "VIVT")
                .Replace("VNET", "CIEL")
                .Replace("VCPA", "FIBR")
                .Replace("PRGA", "BRFS")
                .Replace("AMBV4", "ABEV3")
                .Replace("DURA4", "DTEX3");

            var dir = Directory.CreateDirectory(_leanequityfolder + @"daily\");
            var file = dir.FullName + line.Substring(12, 12).Trim().ToLower() + ".csv";

            var output = line.Substring(2, 8) + ",";
            output += Convert.ToInt64(line.Substring(56, 13)) * 100 + ",";
            output += Convert.ToInt64(line.Substring(69, 13)) * 100 + ",";
            output += Convert.ToInt64(line.Substring(82, 13)) * 100 + ",";
            output += Convert.ToInt64(line.Substring(108, 13)) * 100 + ",";
            output += Convert.ToInt64(line.Substring(152, 18)) + "\r\n";

            File.AppendAllText(file, output);
        }

        private static async Task MakeMinuteFilesFromTickFiles()
        {
            Console.WriteLine();
            var dirs = new DirectoryInfo(_leanequityfolder + @"tick\").GetDirectories();

            foreach (var dir in dirs)
            {
                var files = dir.GetFiles("*.csv");

                if (files.Count() == 0) continue;
                var outdir = Directory.CreateDirectory(dir.FullName.Replace("tick", "minute"));

                foreach (var file in files)
                {
                    var lines = File.ReadAllLines(file.FullName, Encoding.ASCII).ToList();

                    var lastmin = (new DateTime()).AddMilliseconds(Convert.ToInt32(lines.Last().Split(',')[0]));

                    var currsec = (new DateTime()).AddMilliseconds(Convert.ToInt32(lines.First().Split(',')[0]));

                    currsec = currsec
                        .AddSeconds(-currsec.TimeOfDay.Seconds)
                        .AddMilliseconds(-currsec.TimeOfDay.Milliseconds);

                    while (currsec < lastmin)
                    {
                        currsec = currsec.AddMinutes(1);
                        var prev = lines.FindAll(l => (new DateTime()).AddMilliseconds(Convert.ToInt32(l.Split(',')[0])) < currsec);

                        if (prev.Count == 0) continue;

                        lines.RemoveRange(0, prev.Count);

                        var bar = currsec.AddMinutes(-1).TimeOfDay.TotalMilliseconds.ToString() + "," +
                            prev.First().Split(',')[1] + "," +
                            prev.Max(p => Convert.ToInt32(p.Split(',')[1])).ToString() + "," +
                            prev.Min(p => Convert.ToInt32(p.Split(',')[1])).ToString() + "," +
                            prev.Last().Split(',')[1] + "," +
                            prev.Sum(p => Convert.ToInt32(p.Split(',')[2])).ToString() + Environment.NewLine;

                        var newfile = file.FullName.Replace("tick", "minute").Replace("Tick", "Minute");
                        File.AppendAllText(newfile, bar);

                        Console.WriteLine(DateTime.Now + ": " + newfile + " criado.");
                    }
                }

            }
        }

        static void ZipALLRaw()
        {
            Console.WriteLine();
            var dirs = new DirectoryInfo(_leanequityfolder + @"minute\").GetDirectories().ToList();
            dirs.AddRange(new DirectoryInfo(_leanequityfolder + @"tick\").GetDirectories().ToList());

            foreach (var dir in dirs)
            {
                var files = dir.GetFiles("*.csv");

                foreach (var file in files)
                {
                    var zipfile = dir.FullName + @"\" + file.Name.Substring(0, 9) + "trade.zip";

                    using (var z = new FileStream(zipfile, FileMode.Create))
                    using (var a = new ZipArchive(z, ZipArchiveMode.Create))
                        a.CreateEntryFromFile(file.FullName, file.Name);

                    //
                    File.Delete(file.FullName);
                    Console.WriteLine(file.Name + "zipped and deleted");
                }


            }

        }
    }

}                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                