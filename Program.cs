﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Coflnet;
using dev;
using Hypixel.NET;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RestSharp;
using SixLabors.ImageSharp.Formats;
using WebSocketSharp.Net;
using static hypixel.Enchantment;

namespace hypixel
{

    class Program {
        static string apiKey = "1cef3df6-ff0e-49a6-b084-27295c330d4a";

        public static bool displayMode = false;

        public static int usersLoaded = 0;

        /// <summary>
        /// Is set to the last time the ip was rate limited by Mojang
        /// </summary>
        /// <returns></returns>
        private static DateTime BlockedSince = new DateTime (0);

        public static int RequestsSinceStart { get; private set; }

        public static event Action onStop;

        static void Main (string[] args) {

            Console.CancelKeyPress += delegate {
                Console.WriteLine ("\nAbording");
                onStop?.Invoke ();

                var cacheCount = StorageManager.CacheItems;
                StorageManager.Stop ();
                Indexer.Stop ();

                var t = StorageManager.Save ();
                Console.WriteLine ("Saving");
                ItemPrices.Instance.Save ();
                t.Wait ();
                Console.WriteLine ($"Saved {cacheCount}");
            };

            if (args.Length > 0) {
                FileController.dataPaht = args[0];
                Directory.CreateDirectory (FileController.dataPaht);
                Directory.CreateDirectory (FileController.dataPaht + "/users");
                Directory.CreateDirectory (FileController.dataPaht + "/auctions");

                if (args.Length > 1) {
                    runSubProgram (args[1][0]);
                    return;
                }
            }

            displayMode = true;

            while (true) {
                //try {

                Console.WriteLine ("1) List Auctions");
                Console.WriteLine ("2) List Bids");
                Console.WriteLine ("3) Display");
                Console.WriteLine ("4) List Won Bids");
                Console.WriteLine ("5) Search For auction");
                Console.WriteLine ("6) Avherage selling price in the last 2 weeks");
                Console.WriteLine ("9) End");

                var res = Console.ReadKey ();
                if (runSubProgram (res.KeyChar))
                    return;

                //} catch(Exception e)
                //{
                //    Console.WriteLine("Error Occured: "+ e.Message);
                //    throw e;
                //}
            }

        }

        /// <summary>
        /// returns true if application should be closed
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        static bool runSubProgram (char mode) {
            switch (mode) {
                case 't':
                    // test

                    break;
                case 'b':
                    var key = System.Text.Encoding.UTF8.GetString (FileController.ReadAllBytes ("apiKey")).Trim ();
                    BazaarUpdater.NewUpdate (key);
                    break;
                case 'f':
                    FullServer ();
                    break;

                case 's':
                    var server = new Server ();
                    server.Start ();
                    break;
                case 'u':
                    var updater = new Updater (apiKey);
                    updater.Update ();
                    break;
                case '1':
                    var user = ReadUser ();
                    foreach (var item in user.auctionIds) {
                        Console.WriteLine (StorageManager.GetOrCreateAuction (item).ItemName);
                    }
                    break;
                case '2':
                    using (var context = new HypixelContext ()) {

                        var auctionI = context.Auctions.Include (ac => ac.Bids).Where (ac => ac.Id == 169333).First ();
                        var ares = (new BidComparer ()).Equals (auctionI.Bids[0], auctionI.Bids[2]);

                        var result = context.BazaarPull
                            .Include (p => p.Products)
                            .ThenInclude (p => p.BuySummery)
                            .Include (p => p.Products)
                            .ThenInclude (p => p.QuickStatus)

                            .Where (p => p.Timestamp > new DateTime (2020, 5, 22, 19, 32, 0)).ToList ();

                        var a = result.Where (b => b.Products.Exists (p => p.ProductId == "SUPERIOR_FRAGMENT"))
                            .ToList ();
                        var res = new List<ProductInfo> ();
                        foreach (var item in a) {
                            res.AddRange (item.Products.Where (p => p.ProductId == "SUPERIOR_FRAGMENT"));
                        }
                        foreach (var item in res) {
                            if (item == null || item.BuySummery == null) {
                                Console.WriteLine ("null");
                                continue;
                            }
                            if (item.BuySummery.Count == 0) {
                                Console.WriteLine ("empty");
                                continue;
                            }
                            Console.WriteLine ($"Top: {item?.BuySummery?.First().PricePerUnit}  {item?.QuickStatus?.BuyPrice} {item?.QuickStatus?.BuyVolume}");
                        }

                    }
                    break;
                case '3':
                    DisplayUser ();
                    break;

                case '4':
                    var targetUser = ReadUser ();
                    Console.WriteLine ("Won Auctions");
                    foreach (var item in GetBiddedAuctions (targetUser)) {
                        if (item.Bids[item.Bids.Count - 1].Bidder == targetUser.uuid)
                            Console.WriteLine ($"{item.ItemName} for {item.HighestBidAmount}, Ended: {item.End < DateTime.UtcNow.ToLocalTime()}, End {item.End}");

                    }
                    break;
                case '5':
                    var itemName = "Enchanted Glowstone";
                    var time = DateTime.Parse ("12.12.2019 9:53:55");

                    var matchedAuctions = StorageManager.GetOrCreateItemRef (itemName, true).auctions;
                    //.Where(a=>a.End > (time-new TimeSpan(24,0,5)) );//&& a.End < time+new TimeSpan(1,0,5) );

                    Console.WriteLine ("Searching");

                    foreach (var item in matchedAuctions) {
                        var a = StorageManager.GetOrCreateAuction (item.uuId);

                        if (a.Count == 0 || a.HighestBidAmount / a.Count > 10 || a.HighestBidAmount == 0)
                            continue;

                        if (a.Bids == null || a.Bids.Count == 0)
                            Console.WriteLine ($"{a.ItemName}(x{a.Count}) for {a.HighestBidAmount} End {a.End} ({a.AuctioneerId}) to noone");
                        else
                            Console.WriteLine ($"{a.ItemName}(x{a.Count}) for {a.HighestBidAmount} End {a.End} ({a.AuctioneerId}) to {a.Bids.Last().Bidder} id: {item.uuId} ");

                    }

                    break;
                case '6':
                    var targetItemName = Console.ReadLine ();
                    Console.WriteLine ($"Overview for the last 2 weeks for {targetItemName}");
                    var twoWeeksAgo = DateTime.UtcNow.ToLocalTime ().Subtract (new TimeSpan (14, 0, 0, 0));
                    var collection = AuctionsForItem (targetItemName, default (DateTime), DateTime.MaxValue)
                        .Where (
                            item => {
                                if (item == null) {
                                    Console.WriteLine ("ein null :/");
                                }
                                return item != null && item.Bids != null && item.Bids.Count > 0
                                    //&& item.Start > twoWeeksAgo
                                    &&
                                    item.End < DateTime.UtcNow.ToLocalTime ();
                            });

                    Console.WriteLine (collection.First ().ItemName);
                    CalculateAggregates (collection);
                    break;
                case '7':
                    displayMode = false;
                    StorageManager.Migrate ();
                    var auction = StorageManager.GetOrCreateAuction ("00e45a19c27848829612be8edf53bd71");
                    Console.WriteLine (auction.ItemName);
                    //Console.WriteLine(ItemReferences.RemoveReforges("Itchy Bat man"));
                    break;
                case 'a':
                    Indexed ();
                    break;
                case 'i':
                    Indexer.BuildIndexes ();
                    break;
                case 'p':
                    Indexer.LastHourIndex ();
                    //StorageManager.Migrate();
                    break;
                case 'n':
                    Console.WriteLine (NBT.Pretty ("H4sIAAAAAAAAAN1WTW/bRhAdx04iKy2cNHUbNy26aZMmjkObpPgh8abIsiRUsgxbbo/CklxKhCnSIFd23EPPPfXSa3Mr4P/ha/9FfkjTWUrWh+UYbhAUaHWgyNmZ2dmd995uFmAR5vwsACzcgBu+OyfNwc1S1A/5XBbmOe0swgILnS6I3xzc2Q/tmNEDagdsbh4Wq77LtgLaSXD0ryzcdv3kMKAnGFSPYpZB6+fw/OzULMU+J6UuDR1mkbNTZ814gn/5Z/vhYcBoQkNO0LQ69t2kPdoZ+uZmfXPoC/ApelcZDXhXONI1UyfVHfgErZvMY2HCBmZDhlW01ULOgsDvsGERdC03m1ekvXN2WqjE0THvkh/gG4ysxDiYnE+BJb15/QcZTIyj67CEATtxxJnD/SjEoG+ngxQRRN+8/oUM60qjPsaoVjeKw4TUajX4ehxDhbsui2WbTrpphEdp0pjZ2BtXjBfS4cgjbrpVxBX1wAO02dQ5IJQT3mX4x/GLxeuiFWenxlY/CMge4+RlFPYTi7RoeDDcHQcbm7Bken1iDvtksAp8hWf4oIMCptdDJryIF8XwBF/YEYuF2c69kGVZ5Cr3WEwDNyF+SE6ifgxfijVGQTDYu3XSoK9ITpcJo05XFH337FQ/Ow3KO7USedlstvbm4Sb64wTwFjKwsE17DB6h00Qbh5OQYtyLYlxpxBPIwlL5FY9pkfPYt/ucJZnzRLdlS9V1S85Aphe5vuezGLL9UboMLEax3/HDFu3Acmm3uNWqbVfald3aZrvUrNfLpVZGUAfulxvl3WJ9s13cbTR322m1WfhI0AfT9Bi2dh5u8bTluLL5ecgejlCDhps42klxJz7E4vp9TPuY2qajFFxZMry8K2kypRIt6Kak5jXbcBWPFTwHc9HwyA/afWyhoCpWzf0eSzjtHcJdc0PRN5QCURRLNkixAXADbg04BuJdMP/FBea/D9sXZ9mXk9H+4BLoVWnCGayMYS/wesxozGJy7OMmYFdNzHDI4h4NcfcGEUgWAbHjrh8wyODrMe6nQApJ3fOtro/o4qxHHBoSm5GYIRw7zH0kCEARS/vbpWaj0dwm9XKlgq0cQ+qnE/ccUl+gb8MPWfw0Ic0+91CT6qyDKOhciaU7iqpaiqpYiixPAef+NHD2qrWtIWyWG7Xt8m67ud/aqrXa5zWNum9rumZQsyCpzCtImucwyWa2KSkF0zY9nRqOqk81e8nYUAzR7LyV097R67UPrPKfwfIFLVbeIcYpHP6xwv7rWrlyCWCLdhLFNjwWMjYUtoIio2J1epSU+jZq54GPSuYKoXyKj45/xKZgnW7M9PLgO/wciar95vdfyeRBNYa6YINIgwhMUyaMD9RSlcdq+RCdJuv5Pq0n3XkZ7uFguhRszND0cGSaOR3la1FqeUKeS9XyXmunXmyVR4R6+9tIo1Ho9YEgYx+GNXYRsogklIErKLUoxFnRTOu6hFoZ6G9zq90oVhrF9riuq9X4UvHNu7KnuQUq5ZS8I2laTpdsXXUk3clrrks9zXa0i1qbU1KtVS1ZIzuX8u/5B+bfcnrcTvFPzWOb0wvOj37C8GqDn6uX8lEcwXuHbIiTNXUiSF1NE89enQx9wsvQ/8/Xpv+oFNybYGa1XG+UWyNW/vznyjkrv0KvtIkXqFllQY/xq2iZVVTDUuWcJY6f8cVp4RizXfvKtHyBqoNCr03TmRvUmLe6q5ieYlIpbzNP0nKaLeGdyZHyCCVN9xzqssLVl6YlvDTJKZEt3ZzhMfwNcS15lTsNAAA\u003d"));
                    //Console.WriteLine (JsonConvert.SerializeObject (ItemDetails.Instance.Items.Where (item => item.Value.AltNames != null && item.Value.AltNames.Count > 3 && !item.Key.Contains("DRAGON")).Select((item)=>new P(item.Value))));
                    break;
                case 'g':
                    var ds = new DataSyncer ();
                    ds.Sync ("e5bac11a8cc04ca4bae539aed6500823");
                    break;
                case 'm':
                    Migrator.Migrate ();
                    break;
                case 'd':
                    DBTest ();
                    break;
                default:
                    return true;
            }
            return false;
        }

        private static void FullServer ()
        {
            Console.WriteLine("\n - Starting FullServer 0.2.1b - \n");


            Updater updater;
            Server server;
            updater = new Updater(apiKey);
            updater.UpdateForEver();

            Console.WriteLine("waiting for db");
            System.Threading.Thread.Sleep(5000);
            WaitForDatabaseCreation();

            var bazzar = new BazaarUpdater();
            bazzar.UpdateForEver(apiKey);
            RunIndexer();

            server = new Server();

            NameUpdater.Run();

            onStop += () =>
            {
                Console.WriteLine("Stopping");
                server.Stop();
                Indexer.Stop();
                updater.Stop();
                bazzar.Stop();
                System.Threading.Thread.Sleep(500);
                Console.WriteLine("done");
            };

            server.Start();

        }

        private static void RunIndexer()
        {
            Task.Run(() =>
            {
                Indexer.MiniumOutput();
                while (true)
                {
                    try
                    {
                        Indexer.LastHourIndex();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"An error occured while indexing {e.Message} {e.InnerException?.Message}");
                    }
                    System.Threading.Thread.Sleep(5000);
                }
            });
        }

        private static void WaitForDatabaseCreation()
        {
            try {
               
            using (var context = new HypixelContext())
            {
                try
                {
                    var testAuction = new SaveAuction()
                    {
                        Uuid = "00000000000000000000000000000000",
                        Enchantments = new List<Enchantment>() { new Enchantment(EnchantmentType.aiming, 0) },
                        Bids = new List<SaveBids>() { new SaveBids() { Amount = 0 } }
                    };
                    context.Auctions.Add(testAuction);
                    context.SaveChanges();
                    context.Auctions.Remove(testAuction);
                    context.SaveChanges();
                }
                catch (Exception)
                {
                    // looks like db doesn't exist yet
                    Console.WriteLine("Waiting for db creating in the background");
                    System.Threading.Thread.Sleep(10000);
                }
                context.Database.EnsureCreated();
            } 
            }catch(Exception e)
            {
                Console.WriteLine($"Waiting for db creating in the background {e.Message} {e.InnerException?.Message}");
                    System.Threading.Thread.Sleep(10000);
            }
        }

        static void DBTest () {
            using (var context = new HypixelContext ()) {
                // Creates the database if not exists
                context.Database.EnsureCreated ();

                var data = new NbtData ("H4sIAAAAAAAAAE1T3W7aSBQ+JO0WqLZ/6nU7Reld3DVg/iL1gkBCTGOHJEASqqoa28dmwD/IHidxql7sA+wj9JoX6BPwKH2Q1R5It6o00pyf73zfOUczRYAC5EQRAHJbsCWc3D85eNiJ0lDmirAtubcNhSPh4KHPvYRQ/xaheD5Pff/kJsQ4D1u6Azuu06w2uKYpTq3lKFW7bClc47ZSd3izbqlNB60K1Q3iaIGxFJgUIC/xVqYxJhvpPDwccz9F+I5ZX51cTlXnsu/bmV4nf3iu+if6bNHQw3FmdfS6HlD+qF0/zlq/YWuSX9T8q2p/OglPUysYq8fVMx+Pzsp2MLo2Z+b85MIU5mx0a1SuMqNrZ5PheG72Rqoxm8zNu/2pORyVzUp/ZtzNNbLVqztPM3qn6mToCLMynp30xoHZnavmxVgY3X5giH7LvVTf0wRFeOSIZOHzrAAPjqMY8xR8AX+uls1OFFhcsgFKCj1fLRvnMsbQk9M9tlpyFZ5RqIsuhgn+jAA8XS3r+9yOQnbI4wBjeEUgOgPhsUCEIgoTdhPF8w3+XfUtvCGD8i5PJMbsZip8ZFSdRWnMROLz0CHWJ8Q6WFcZGxi8vifdT103YXKKjOgDHrKEqB1mZbC7AZCCRkL2j29/sy4PuIfkNYjyXr3yMweP6f5/NlLb2RTjmfCmUrF9Yc+ZjBh3HJISCVugJH+9ENx0ufYDDNM3622ulrXV0j8Y6J08PDB5gPCS2D4eX/us/In0aved0tqfHtzKmLeljIWVSkzy8IiY9NCN4OJLSWYLLO2VBnrPaJul3RK3pbimiMv9BHdLeLso7anv1N0SPcqYgGtFgk3pwa8pfgFtmjYbJegQ/GseClEsPBEOuQdPTkd658Pnzln7cKibvfz6D8H24GBIjacp2TtVTaMfUOOK1WygommtltJqYUOx3Faj0bTLWC9zopQiwETyYAHPan9VVDqsrO6Vm6xtAGzBH/ebh22A/wDOxlWDtAMAAA\u003d\u003d");

                int count = 0;
                Console.Write ("starting");
                foreach (var item in AllAuctions ()) {
                    Console.Write ($"\r {count}  {item.AuctioneerId}");
                    if (!context.Auctions.Contains (item) && item != null) {
                        context.Auctions.Add (item);
                        AddPlayer (context, item.AuctioneerId);
                        if (item.Bids != null)
                            foreach (var player in item.Bids) {
                                AddPlayer (context, player.Bidder);
                            }
                        count++;
                    }
                    if (count % 1000 == 0)
                        context.SaveChanges ();

                    if (count > 10000)
                        break;
                }

                // Saves changes
                context.SaveChanges ();
            }
        }

        public static void AddPlayer (HypixelContext context, string uuid, string name = null) {
            var p = new Player () { UuId = uuid };
            if (context.Players.Find (p.UuId) == null && p.UuId != null) {
                p.Name = name;
                context.Players.Add (p);

            }
        }

        public static void AddPlayers (HypixelContext context, List<string> ids) {
            ids = ids.Distinct ().ToList ();
            var found = context.Players.Where (p => ids.Contains (p.UuId)).ToDictionary (p => p.UuId);
            var names = new ConcurrentDictionary<string, string> ();
            try {
                Parallel.ForEach (ids, id => {
                    if (!found.ContainsKey (id)) {
                        names[id] = GetPlayerNameFromUuid (id);
                    }

                });
            } catch (Exception) {
                Console.WriteLine ("getting names failed");
            }
            foreach (var item in ids) {
                names.TryGetValue (item, out string name);
                AddPlayer (context, item, name);

            }
        }

        static IEnumerable<SaveAuction> AllAuctions () {
            var path = "nauctions";
            foreach (var item in FileController.DirectoriesNames ("*", path)) {
                var dirName = Path.GetFileName (item);
                foreach (var fileName in Directory.GetFiles (item).Select (Path.GetFileName)) {
                    var compactPath = $"{path}/{dirName}/{fileName}";

                    Dictionary<string, SaveAuction> auctions = null;
                    try {
                        auctions = FileController.LoadAs<Dictionary<string, SaveAuction>> (compactPath);

                        FileController.Move (compactPath, compactPath.Replace ("nauctions", "importedAuctions"));
                    } catch (Exception e) {

                        Console.WriteLine ($"Skipping {compactPath} because of {e.Message}");
                    }
                    if (auctions == null)
                        continue;
                    foreach (var auction in auctions) {
                        yield return auction.Value;
                    }

                }
            }

        }

        static void Indexed () {
            long count = 0;

            foreach (var item in FileController.FileNames ("*", "items")) {
                var itemsAuctions = StorageManager.GetOrCreateItemRef (item);
                if (itemsAuctions == null) {
                    Console.WriteLine ($"{itemsAuctions.Name} emtpy");
                    continue;
                }

                //Console.WriteLine($"{itemsAuctions.Name} has {itemsAuctions.auctionIds.Count}");

                count += itemsAuctions.auctions.Count;
            }
            Console.WriteLine ($"Total: {count}");
        }

        static void CalculateAggregates (IEnumerable<SaveAuction> collection) {
            Console.WriteLine ();
            long sum = 0;
            int count = 0;
            long min = long.MaxValue;
            long max = 0;
            foreach (var item in collection) {

                var perPice = item.HighestBidAmount / item.Count;

                if (perPice > 100000)
                    continue;
                count++;
                sum += perPice;
                if (perPice < min) {
                    min = perPice;
                }
                if (perPice > max) {
                    max = perPice;
                }
                Console.Write ($"\rAvg: {sum/count} Sum: {sum} Min: {min} Max: {max} Count: {count} ");
            }
            Console.WriteLine ();
        }

        static void DisplayUser () {
            var displayUser = ReadUser ();

            Console.WriteLine ("Bids");
            foreach (var item in displayUser.Bids) {
                var a = StorageManager.GetOrCreateAuction (item.auctionId);
                if (a.Bids == null || a.Bids.Count == 0) {
                    continue;
                }
                var highestOwn = a.Bids.Where (bid => bid.Bidder == displayUser.uuid)
                    .OrderByDescending (bid => bid.Amount).FirstOrDefault ();

                if (highestOwn == null) {
                    continue;
                }

                Console.WriteLine ($"On {a.ItemName} {highestOwn.Amount} \tTop {highestOwn.Amount == a.HighestBidAmount} {highestOwn.Timestamp} ({item.auctionId.Substring(0,10)})");
            }

            Console.WriteLine ("Auctions:");
            foreach (var item in displayUser.auctionIds) {
                var a = StorageManager.GetOrCreateAuction (item);
                if (a.Enchantments != null && a.Enchantments.Count > 0) {
                    // enchanted is only one item
                    Console.WriteLine ($"{a.ItemName}  for {a.HighestBidAmount} End {a.End} ({item.Substring(0,10)})");
                    foreach (var enachant in a.Enchantments) {
                        Console.WriteLine ($"-- {enachant.Type} {enachant.Level}");
                    }
                } else
                    // not enchanted may be multiple (Count)
                    Console.WriteLine ($"{a.ItemName} (x{a.Count}) for {a.HighestBidAmount} End {a.End} ({item.Substring(0,10)})");
            }
        }

        static User ReadUser () {
            return StorageManager.GetOrCreateUser (Console.ReadLine ().Trim ());
        }

        public static IEnumerable<SaveAuction> AuctionsForItem (string itemName, DateTime start, DateTime end) {
            itemName = itemName.ToLower ();
            return StorageManager.GetAuctionsWith (itemName, start, end);
        }

        /// <summary>
        /// Gets all the Auctions the User bidded on/in
        /// </summary>
        /// <param name="target">The user to search bids for</param>
        /// <returns></returns>
        static IEnumerable<SaveAuction> GetBiddedAuctions (User target) {
            var bids = new List<SaveAuction> ();
            foreach (var bidReference in target?.Bids) {
                var seller = StorageManager.GetOrCreateUser (bidReference.sellerId);
                yield return seller.auctions[bidReference.auctionId];
            }
        }

        static bool IsAnotherInstanceRunning (string typeId = "lock", bool placeLockIfNonexisting = false) {
            var lastUpdate = new DateTime (1970, 1, 1);
            if (FileController.Exists ("lastUpdate"))
                lastUpdate = FileController.LoadAs<DateTime> ("lastUpdate").ToLocalTime ();

            var lastUpdateStart = new DateTime (0);
            if (FileController.Exists ("lastUpdateStart"))
                lastUpdateStart = FileController.LoadAs<DateTime> ("lastUpdateStart").ToLocalTime ();

            Console.WriteLine ($"{lastUpdateStart > lastUpdate} {DateTime.Now - lastUpdateStart}");
            FileController.SaveAs ("lastUpdateStart", DateTime.Now);

            return false;
        }

        static void RemoveFileLock (string typeId) {
            FileController.Delete (typeId + "lock");
        }

        static void pastAuctions (string name) {
            var hypixel = new HypixelApi (apiKey, 300);
            var getProfilesByName = hypixel.GetSkyblockProfilesByName ("ekwav");
            foreach (var profile in getProfilesByName) {
                Console.WriteLine ("---" + profile.Profile.ProfileId);
                var auctionsByPlayerName = hypixel.GetAuctionsByProfileId (profile.Profile.ProfileId);

                foreach (var item in auctionsByPlayerName.Auctions) {
                    Console.WriteLine ($"->{item.ItemName} {item.HighestBidAmount} {item.Start} {item.End}");
                }
            }
        }

        /// <summary>
        /// Downloads username for a given uuid from mojang.
        /// Will return null if rate limit reached.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns>The name or null if error occurs</returns>
        public static string GetPlayerNameFromUuid (string uuid) {
            if (DateTime.Now.Subtract (new TimeSpan (0, 10, 0)) < BlockedSince && RequestsSinceStart >= 2000) {
                //Console.Write("Blocked");
                // blocked
                return null;
            } else if (RequestsSinceStart >= 2000) {
                Console.Write ("\tFreed 2000 ");
                RequestsSinceStart = 0;
            }

            //Create the request
            RestClient client = null;
            RestRequest request;
            int type = 0;

            if (RequestsSinceStart == 600) {
                BlockedSince = DateTime.Now;
            }

            if (RequestsSinceStart < 600) {
                client = new RestClient ("https://api.mojang.com/");
                request = new RestRequest ($"user/profiles/{uuid}/names", Method.GET);
            } else if (RequestsSinceStart < 1500) {
                client = new RestClient ("https://mc-heads.net/");
                request = new RestRequest ($"/minecraft/profile/{uuid}", Method.GET);
                type = 1;
            } else {
                client = new RestClient ("https://minecraft-api.com/");
                request = new RestRequest ($"/api/uuid/pseudo.php?uuid={uuid}", Method.GET);
                type = 2;
            }

            RequestsSinceStart++;

            //Get the response and Deserialize
            var response = client.Execute (request);

            if (response.Content == "") {
                return null;
            }

            if (response.StatusCode != System.Net.HttpStatusCode.OK) {
                //Console.WriteLine("Blocked");
                //BlockedSince = DateTime.Now;
                return null;
            }

            if (type == 2) {
                return response.Content;
            }

            dynamic responseDeserialized = JsonConvert.DeserializeObject (response.Content);

            if (responseDeserialized == null) {
                return null;
            }

            switch (type) {
                case 0:
                    return responseDeserialized[responseDeserialized.Count - 1]?.name;
                case 1:
                    return responseDeserialized.name;
            }

            return responseDeserialized.name;
        }

    }

}