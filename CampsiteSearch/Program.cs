using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Net;
using System.Configuration;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;

namespace CampsiteSearch
{
    class Program
    {
        public HttpClient client = new HttpClient();

        List<Park> parksToSearch = new List<Park>();

        DateTime desiredDate;
        bool specificDate;
        List<DateTime> specificDates = new List<DateTime>();

        string username;
        string password;

        DateTime mostRecentReservation = new DateTime(0);

        static Program program;
        static void Main(string[] args)
        {
            program = new Program();
            program.client.Timeout = new TimeSpan(0, 3, 0);
            //Stopwatch sw = Stopwatch.StartNew();
            string parksList;
            try
            {
                program.username = ConfigurationManager.AppSettings["username"];
                program.password = ConfigurationManager.AppSettings["password"];
                parksList = ConfigurationManager.AppSettings["parkIds"];
            }
            catch (Exception e)
            {
                Console.WriteLine("failed while parsing App.config. Exception: {0}", e.Message);
                Console.Read();
                return;
            }
            try
            {
                string date = ConfigurationManager.AppSettings["start date"];
                if(!DateTime.TryParse(date, out program.desiredDate))
                {
                    throw new Exception();
                }
            }
            catch(Exception)
            {
                Console.WriteLine("you didnt enter a date, or your date was invalid. using today's date as desired date");
                program.desiredDate = DateTime.Now;
            }
            if(ConfigurationManager.AppSettings["dates"]!=null)
            {
                string strDates = ConfigurationManager.AppSettings["dates"];
                program.specificDate = true;
                List<string> dates = new List<string>();
                dates = strDates.Split(',').ToList();
                foreach (string _date in dates)
                {
                    _date.Trim();
                    DateTime dateTime;
                    if(!DateTime.TryParse(_date, out dateTime))
                    {
                        Console.WriteLine("one of your dates, {0}, couldnt be parsed. please check and fix your formatting", _date);
                        continue;
                    }
                    else
                    {
                        program.specificDates.Add(dateTime);
                    }
                }
            }
            List<string> parks = new List<string>();
            parks = parksList.Split(',').ToList();
            foreach (string park in parks)
            {
                park.Trim();
                int parkId;
                if(int.TryParse(park, out parkId))
                {
                    program.parksToSearch.Add(new Park(parkId));
                }
                else
                {
                    Console.WriteLine("failed parsing parkid \"{0}\" to int", park);
                }
            }
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                program.LoginLoop();
            }).Start();
            //perform initial login
            program.DoLogin().Wait();
            program.LoadCampsites().Wait();
            while (true)
            {
                Console.WriteLine("performing new search");
                program.GetAvailability().Wait();
                Thread.Sleep(5000);
            }
            //sw.Stop();
            //Console.WriteLine("getting campsite info took {0} ms", sw.ElapsedMilliseconds);
            Console.ReadLine();
        }
        void LoginLoop()
        {
            //every 30 minutes refresh our login
            while (true)
            {
                //login will destroy any items in cart
                TimeSpan cartWaitTime = new TimeSpan(0, 20, 0);
                //if the time now - the time of most recent reservation is greater than 20 minutes, then we dont have to worry about destroying our login session
                if (DateTime.Now - mostRecentReservation > cartWaitTime)
                {
                    Thread.Sleep(1800000);//wait 30 minutes
                    program.DoLogin().Wait();
                }
                //else we will sleep for 20 minutes so as not to remove items in cart
                else
                {
                    Thread.Sleep(cartWaitTime);
                }
            }
        }
        async Task DoLogin()
        {
            string loginurl = string.Format(SiteLinks.login, program.username, program.password);
            Process.Start(loginurl);
            Console.WriteLine("Logged you in. Please keep your browser open.");
            Thread.Sleep(10000);
        }
        async Task<bool> ReserveCampsite(DateTime reservationDate, Campsite siteToReserve, int parkId)
        {
            string reservationurl = string.Format(SiteLinks.bookCampsite, parkId, siteToReserve.siteId, reservationDate.Month, reservationDate.Day, reservationDate.Year, 1);
            //var response = await client.GetStringAsync(reservationurl);
            Process.Start(reservationurl);
            return true;
        }
        async Task GetAvailability()
        {
            //Dictionary<int, List<KeyValuePair<int, Task<string>>>> responses = new Dictionary<int, List<KeyValuePair<int, Task<string>>>>();
            foreach (Park park in parksToSearch)
            {
                List<KeyValuePair<Campsite, Task<string>>> htmlResponses = new List<KeyValuePair<Campsite, Task<string>>>();
                foreach(var entry in park.campsites)
                {
                    string html;
                    string url = string.Format(SiteLinks.campsiteInfo, entry.Key, desiredDate.Month, desiredDate.Day, desiredDate.Year, park.parkId);
                    //Stopwatch sw = Stopwatch.StartNew();
                    htmlResponses.Add(new KeyValuePair<Campsite,Task<string>>(entry.Value, client.GetStringAsync(url)));
                    Thread.Sleep(1);
                }
                foreach(var htmlResponse in htmlResponses)
                {
                    string html;
                    try
                    {
                        html = await htmlResponse.Value;
                    }
                    catch (Exception e)
                    { 
                        Console.WriteLine("exception thrown while getting availibility: {0}",e.Message);
                        break;
                    }
                    HtmlAgilityPack.HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    HtmlNode calendar = doc.DocumentNode.SelectSingleNode("//table[@id='calendar']");
                    if(calendar==null)
                    {
                        Console.WriteLine("{0:[HH:mm:ss]} couldnt get calendar for campsite id #{1}",DateTime.Now, htmlResponse.Key.siteId);
                        continue;
                    }
                    HtmlNode body = calendar.SelectSingleNode(".//tbody");
                    if (body == null)
                    {
                        continue;
                    }
                    HtmlNode row = body.SelectSingleNode(".//tr");
                    if(row==null)
                    {
                        continue;
                    }
                    int iterator = 0;
                    foreach (HtmlNode avail in row.SelectNodes(".//td"))
                    {
                        if (avail != null)
                        {
                            bool available = false;
                            TimeSpan ts = new TimeSpan(iterator, 0, 0, 0); //add one day for each row in the table
                            DateTime reservationDate = desiredDate.Date + ts;
                            foreach (var attribute in avail.Attributes)
                            {
                                if (attribute.Name == "title")
                                {
                                    if (attribute.Value == "Available")
                                    {
                                        if (!specificDate)
                                        {
                                            available = true;
                                            Console.WriteLine("attempting to book campsite w/ id #{0}, available on {1:MM/dd/yy}", htmlResponse.Key.siteId, reservationDate);
                                            bool booked = await ReserveCampsite(reservationDate, htmlResponse.Key, park.parkId);
                                            if (booked)
                                            {
                                                mostRecentReservation = DateTime.Now;
                                                Console.WriteLine("{2:[hh:mm:ss]}reserved campsite w/ id #{0} on account {1}, you have 15 minutes to confirm in your browser.", htmlResponse.Key.siteId, username, DateTime.Now);
                                            }
                                        }
                                        else
                                        {
                                            bool matchedDate = false;
                                            foreach(var dateTime in specificDates)
                                            {
                                                if(dateTime.Month==reservationDate.Month&&dateTime.Day==reservationDate.Day)
                                                {
                                                    matchedDate = true;
                                                    break;
                                                }
                                            }
                                            if (matchedDate)
                                            {
                                                available = true;
                                                Console.WriteLine("attempting to book campsite w/ id #{0}, available on {1:MM/dd/yy}", htmlResponse.Key.siteId, reservationDate);
                                                bool booked = await ReserveCampsite(reservationDate, htmlResponse.Key, park.parkId);
                                                if (booked)
                                                {
                                                    Console.WriteLine("{2:[hh:mm:ss]}reserved campsite w/ id #{0} on account {1}, you have 15 minutes to confirm in your browser.", htmlResponse.Key.siteId, username, DateTime.Now);
                                                }
                                            }
                                            else
                                            {
                                                Console.WriteLine("skipping campsite w/ id #{0}, available on {1:MM/dd/yy} because it did not meet your specific date", htmlResponse.Key.siteId, reservationDate);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        //Console.WriteLine("campsite #{0}, is not available on {1:MM/dd/yy}", htmlResponse.Key.siteId, reservationDate);
                                    }
                                }
                            }
                            if (htmlResponse.Key.availibility.ContainsKey(reservationDate))
                            {
                                htmlResponse.Key.availibility[reservationDate] = available;
                            }
                            else
                            {
                                htmlResponse.Key.availibility.Add(reservationDate, available);
                            }
                            iterator++;
                        }
                    }
                }
                //if(responses.ContainsKey(new KeyValuePair<int, int>(park.parkId, entry.Key)))
            }
        }
        async Task LoadCampsites()
        {
            foreach (Park park in parksToSearch)
            {
                int campsiteCount;
                string html = "";
                string searchCampsitesUrl = string.Format(SiteLinks.searchCampsites, park.parkId, 0);
                Process.Start(searchCampsitesUrl);
                Console.WriteLine("\nVisiting campground page w/ id#{0} to get verified cookie.\nFeel free to close the campground page once it loads.\n", park.parkId);
                Thread.Sleep(5000);
                try
                {
                    html = await client.GetStringAsync(searchCampsitesUrl);
                }
                catch(Exception e)
                {
                    Console.WriteLine("exception thrown while loading campsite ids: {0}", e);
                    Thread.Sleep(10000);
                    Environment.Exit(420);
                }
                HtmlAgilityPack.HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);
                HtmlNode campsiteCountNode = doc.DocumentNode.SelectSingleNode("//div[@class='matchSummary']");
                if (campsiteCountNode == null)
                {
                    Console.WriteLine("Error while parsing number of campsites. Setting count to 500.");
                    campsiteCount = 500;
                }
                else
                {
                    string strcampsiteCount = campsiteCountNode.InnerText;
                    Match matchcampsiteCount = Regex.Match(strcampsiteCount, @"\d+");
                    if (!int.TryParse(matchcampsiteCount.Value, out campsiteCount))
                    {
                        Console.WriteLine("Error while parsing number of campsites. Setting count to 500.");
                        campsiteCount = 500;
                    }
                }
                int pages = ((campsiteCount - 1) / 25) + 1;
                for (int i = 0;i<pages;i++)
                {
                    try
                    {
                        html = await client.GetStringAsync(string.Format(SiteLinks.searchCampsites, park.parkId, i * 25));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("exception thrown while loading campsite ids: {0}", e);
                        Thread.Sleep(10000);
                        Environment.Exit(420);
                    }
                    HtmlAgilityPack.HtmlDocument _doc = new HtmlDocument();
                    _doc.LoadHtml(html);
                    foreach(HtmlNode siteListLabel in _doc.DocumentNode.SelectNodes("//div[@class='siteListLabel']"))
                    {
                        if (siteListLabel != null)
                        {
                            int siteId = 0;
                            HtmlNode nodeA = siteListLabel.SelectSingleNode(".//a");
                            if (nodeA != null)
                            {
                                foreach (var attribute in nodeA.Attributes)
                                {
                                    if (attribute.Name == "href")
                                    {
                                        Match matchsiteId = Regex.Match(attribute.Value, @"(?<=siteId=)\d+");
                                        if (!int.TryParse(matchsiteId.Value, out siteId))
                                        {
                                            //Console.WriteLine("Failed getting siteId from a campsite on page {0}", i);
                                            siteId = 0;
                                        }
                                        break;
                                    }
                                }
                                if (siteId != 0)
                                {
                                    if (!park.campsites.ContainsKey(siteId))
                                    {
                                        park.campsites.Add(siteId, new Campsite(siteId));
                                    }
                                    else
                                    {
                                        park.campsites[siteId] = new Campsite(siteId);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
