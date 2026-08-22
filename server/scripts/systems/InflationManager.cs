using Max35050.Net;

namespace Max35050.Systems;

public static class InflationManager
{
    public static async Task Initialize()
    {
        _ = Task.Run(async () => InflationWorker());
    }
    public static async Task InflationWorker(
    CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Wait until the next full UTC hour
                DateTime now = DateTime.UtcNow;

                DateTime nextHour = new DateTime(
                    now.Year,
                    now.Month,
                    now.Day,
                    now.Hour,
                    0,
                    0,
                    DateTimeKind.Utc
                ).AddHours(1);

                TimeSpan timeUntilNextHour = nextHour - now;

                await Task.Delay(timeUntilNextHour, cancellationToken);

                List<dynamic> products =
                    await Server.db.Collections("products").GetFullList();

                List<decimal> productPrices = new();

                foreach (dynamic product in products)
                {
                    try
                    {
                        decimal price = product.price.ToObject<decimal>();

                        // Ignore invalid prices
                        if (price > 0)
                        {
                            productPrices.Add(price);
                        }
                    }
                    catch
                    {
                    }
                }

                if (productPrices.Count == 0)
                {
                    continue;
                }

                List<decimal> filteredPrices =
                    RemoveOutliersUsingIqr(productPrices);

                if (filteredPrices.Count == 0)
                {

                    continue;
                }

                decimal totalPrice = filteredPrices.Sum();
                decimal averagePrice = totalPrice / filteredPrices.Count;

                await Server.db.Collections("stats").Create(new
                {
                    key = "inflation",
                    value = averagePrice,
                    timestamp = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc)
                });
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Prevent a tight retry loop
                await Task.Delay(
                    TimeSpan.FromMinutes(1),
                    cancellationToken
                );
            }
        }
    }

    private static List<decimal> RemoveOutliersUsingIqr(
        IEnumerable<decimal> prices)
    {
        decimal[] sortedPrices = prices
            .Where(price => price > 0)
            .OrderBy(price => price)
            .ToArray();

        // IQR filtering is unreliable for very small datasets
        if (sortedPrices.Length < 4)
        {
            return sortedPrices.ToList();
        }

        decimal firstQuartile = CalculatePercentile(
            sortedPrices,
            0.25m
        );

        decimal thirdQuartile = CalculatePercentile(
            sortedPrices,
            0.75m
        );

        decimal interquartileRange =
            thirdQuartile - firstQuartile;

        decimal lowerLimit =
            firstQuartile - 1.5m * interquartileRange;

        decimal upperLimit =
            thirdQuartile + 1.5m * interquartileRange;

        return sortedPrices
            .Where(price =>
                price >= lowerLimit &&
                price <= upperLimit
            )
            .ToList();
    }

    private static decimal CalculatePercentile(
        decimal[] sortedValues,
        decimal percentile)
    {
        if (sortedValues.Length == 0)
        {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(sortedValues)
            );
        }

        decimal position =
            (sortedValues.Length - 1) * percentile;

        int lowerIndex = (int)Math.Floor(position);
        int upperIndex = (int)Math.Ceiling(position);

        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        decimal fraction = position - lowerIndex;

        return sortedValues[lowerIndex]
            + (
                sortedValues[upperIndex]
                - sortedValues[lowerIndex]
            ) * fraction;
    }
}