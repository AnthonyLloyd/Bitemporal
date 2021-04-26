using System;
using System.Linq;
using Bitemporal;

namespace Demo
{
    public static class ConsoleApp
    {
        public static void Main()
        {
            var snapshot = new FundArchive("data/fund.arc").SnapshotLatest;
            var firstDate = new Date(new DateTime(2015, 6, 05));
            var lastDate = new Date(new DateTime(2016, 3, 21));
            var currentDate = new Date(new DateTime(2015, 10, 20));
            var currentFund = "ALL";

            while (true)
            {
                var asset = snapshot[currentDate].AssetCollection;
                var fundAsset = asset[new(1)];

                if (!"ALL".Equals(currentFund, StringComparison.OrdinalIgnoreCase))
                {
                    bool found = false;
                    foreach (var p in fundAsset.Positions)
                    {
                        if (p.Asset.Name.Equals(currentFund, StringComparison.OrdinalIgnoreCase))
                        {
                            fundAsset = p.Asset;
                            currentFund = p.Asset.Name;
                            found = true;
                            break;
                        }
                    }
                    if (!found) currentFund = "ALL";
                }

                if ("ALL".Equals(currentFund, StringComparison.OrdinalIgnoreCase))
                {
                    var positions = fundAsset.Positions.ToList();
                    Console.WriteLine("\nDate: \u001b[32;1m" + currentDate + "\u001b[0m Funds: \u001b[36;1m" + positions.Count + "\u001b[0m\n");
                    foreach (var p in positions)
                    {
                        Console.Write(p.Asset.Name);
                        Console.Write(" ");
                    }
                    Console.WriteLine();
                }
                else
                {
                    var positions = fundAsset.Positions.Where(i => i.Quantity != 0.0).ToList();

                    Console.WriteLine("\nDate: \u001b[32;1m" + currentDate + "\u001b[0m Fund: \u001b[36;1m" + currentFund + "\u001b[0m\n");

                    int maxISIN = 0, maxTicker = 0, maxName = 0, maxPrice = 0, maxCurrency = 0, maxQuantity = 0, maxCountry = 0, maxSector = 0;

                    foreach (var p in positions)
                    {
                        var a = p.Asset;
                        if (a.ISIN.Length > maxISIN) maxISIN = a.ISIN.Length;
                        if (a.Ticker.Length > maxTicker) maxTicker = a.Ticker.Length;
                        if (a.Name.Length > maxName) maxName = a.Name.Length;
                        if (a.Price.ToString().Length > maxPrice) maxPrice = a.Price.ToString().Length;
                        if (a.Currency.Name.Length > maxCurrency) maxCurrency = a.Currency.Name.Length;
                        if (p.Quantity.ToString().Length > maxQuantity) maxQuantity = p.Quantity.ToString().Length;
                        if (a.Country.Length > maxCountry) maxCountry = a.Country.Length;
                        if (a.Sector.Length > maxSector) maxSector = a.Sector.Length;
                    }

                    foreach (var p in positions)
                    {
                        var a = p.Asset;
                        Console.WriteLine(
                              a.Ticker.PadRight(maxTicker + 2)
                            + a.ISIN.PadRight(maxISIN + 2)
                            + a.Name.PadRight(maxName + 2)
                            + a.Sector.PadRight(maxSector + 2)
                            + a.Price.ToString().PadLeft(maxPrice + 1) + " "
                            + a.Currency.Name.PadRight(maxCurrency + 2)
                            + a.Country.PadRight(maxCountry + 2)
                            + p.Quantity.ToString().PadLeft(maxQuantity + 2));
                    }
                }
                Console.WriteLine();

                Console.Write("Enter for random, fund, all, f, b, f10 b10 for date forward/back: ");
                string? input = Console.ReadLine();

                if (string.IsNullOrEmpty(input))
                {
                    var r = new Random();
                    currentDate = firstDate.Add(r.Next(lastDate - firstDate + 1));
                    var allFunds = snapshot[currentDate].AssetCollection[new(1)].Positions.Select(i => i.Asset.Name).ToList();
                    currentFund = allFunds[r.Next(allFunds.Count)];
                }
                else if (input.StartsWith("f", StringComparison.OrdinalIgnoreCase))
                {
                    int i = 1;
                    if (input.Length == 1 || int.TryParse(input[1..], out i))
                    {
                        currentDate = currentDate.Add(i);
                        if (currentDate.ToDateTime().DayOfWeek == DayOfWeek.Saturday) currentDate = currentDate.Add(2);
                        else if (currentDate.ToDateTime().DayOfWeek == DayOfWeek.Sunday) currentDate = currentDate.Add(1);
                    }
                    else currentFund = input;
                }
                else if (input.StartsWith("b", StringComparison.OrdinalIgnoreCase))
                {
                    int i = 1;
                    if (input.Length == 1 || int.TryParse(input[1..], out i))
                    {
                        currentDate = currentDate.Add(-i);
                        if (currentDate.ToDateTime().DayOfWeek == DayOfWeek.Saturday) currentDate = currentDate.Add(-1);
                        else if (currentDate.ToDateTime().DayOfWeek == DayOfWeek.Sunday) currentDate = currentDate.Add(-2);
                    }
                    else currentFund = input;
                }
                else currentFund = input;

                if (currentDate > lastDate) currentDate = lastDate;
                else if (currentDate < firstDate) currentDate = firstDate;
            }
        }
    }
}