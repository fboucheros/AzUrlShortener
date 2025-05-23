using Azure;
using Azure.Data.Tables;
using Cloud5mins.ShortenerTools.Core.Domain;
using System.Globalization;

using System.Text.Json;

namespace Cloud5mins.ShortenerTools.Core.Service;

public class AzStrorageTablesService(TableServiceClient client) : IAzStrorageTablesService
{

    private TableClient GetUrlsTable()
    {
        client.CreateTableIfNotExists("UrlsDetails");
        TableClient table = client.GetTableClient("UrlsDetails");
        return table;
    }

    private TableClient GetStatsTable()
    {
        client.CreateTableIfNotExists("ClickStats");
        TableClient table = client.GetTableClient("ClickStats");
        return table;
    }

    public async Task<int> GetNextTableId()
    {
        //Get current ID
        TableClient tblUrls = GetUrlsTable();
        NextId entity;

        var check = await tblUrls.GetEntityIfExistsAsync<NextId>("1", "KEY");
        if (check.HasValue)
        {
            var result = await tblUrls.GetEntityAsync<NextId>("1", "KEY");
            entity = result.Value as NextId;
        }
        else
        {
            entity = new NextId
            {
                PartitionKey = "1",
                RowKey = "KEY",
                Id = 1024
            };
        }

        entity.Id++;

        //Update
        await tblUrls.UpsertEntityAsync(entity);

        return entity.Id;
    }

    public async Task<List<ShortUrlEntity>> GetAllShortUrlEntities()
    {
        TableClient tblUrls = GetUrlsTable();
        var lstShortUrl = new List<ShortUrlEntity>();

        // Retreiving all entities that are NOT the NextId entity 
        // (it's the only one in the partion "KEY")
        var queryResult = tblUrls.QueryAsync<ShortUrlEntity>(e => e.RowKey != "KEY");

        await foreach (var emp in queryResult.AsPages())
        {
            foreach (var item in emp.Values)
            {
                if (item.CreatedDate == null)
                {
                    item.CreatedDate = item.Timestamp!.Value.UtcDateTime.ToString("yyyy-MM-dd") ?? string.Empty;
                }
                lstShortUrl.Add(item);
            }
        }

        return lstShortUrl;
    }


    public async Task<ShortUrlEntity> SaveShortUrlEntity(ShortUrlEntity newShortUrl)
    {

        // serializing the collection easier on json shares
        //newShortUrl.SchedulesPropertyRaw = JsonSerializer.Serialize<List<Schedule>>(newShortUrl.Schedules);

        TableClient tblUrls = GetUrlsTable();
        var response = await tblUrls.UpsertEntityAsync<ShortUrlEntity>(newShortUrl);

        var temp = response.Content;
        return newShortUrl;
    }

    public async Task<ShortUrlEntity> UpdateShortUrlEntity(ShortUrlEntity urlEntity)
    {
        ShortUrlEntity originalUrl = await GetShortUrlEntity(urlEntity);
        originalUrl.Url = urlEntity.Url;
        originalUrl.Title = urlEntity.Title;
        originalUrl.SchedulesPropertyRaw = JsonSerializer.Serialize<List<Schedule>>(urlEntity.Schedules);

        return await SaveShortUrlEntity(originalUrl);
    }


    /// <summary>
    /// Returns the ShortUrlEntity of the <paramref name="vanity"/>
    /// </summary>
    /// <param name="vanity"></param>
    /// <returns>ShortUrlEntity</returns>
    public async Task<ShortUrlEntity?> GetShortUrlEntityByVanity(string vanity)
    {
        var tblUrls = GetUrlsTable();
        ShortUrlEntity shortUrlEntity = null;

        var result = tblUrls.QueryAsync<ShortUrlEntity>(e => e.RowKey == vanity);
        await foreach (var entity in result)
        {
            shortUrlEntity = entity;
            break;
        }
        return shortUrlEntity;
    }

    public async Task<ShortUrlEntity> GetShortUrlEntity(ShortUrlEntity row)
    {
        TableClient tblUrls = GetUrlsTable();
        var response = await tblUrls.GetEntityAsync<ShortUrlEntity>(row.PartitionKey, row.RowKey);
        ShortUrlEntity eShortUrl = response.Value as ShortUrlEntity;
        return eShortUrl;
    }


    public async Task<bool> IfShortUrlEntityExistByVanity(string vanity)
    {
        ShortUrlEntity shortUrlEntity = await GetShortUrlEntityByVanity(vanity);
        return (shortUrlEntity != null);
    }


    public async Task<bool> IfShortUrlEntityExist(ShortUrlEntity row)
    {
        TableClient tblUrls = GetUrlsTable();
        var result = await tblUrls.GetEntityIfExistsAsync<ShortUrlEntity>(row.PartitionKey, row.RowKey);
        return result.HasValue;
    }

    public async Task<ShortUrlEntity> ArchiveShortUrlEntity(ShortUrlEntity urlEntity)
    {
        ShortUrlEntity originalUrl = await GetShortUrlEntity(urlEntity);
        originalUrl.IsArchived = true;

        return await SaveShortUrlEntity(originalUrl);
    }



    public async Task<List<ClickStatsEntity>> GetAllStatsByVanity(string vanity, string startDate, string endDate)
    {
        var tblStats = GetStatsTable();
        var sDate = DateOnly.ParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var eDate = DateOnly.ParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var lstUrlStats = new List<ClickStatsEntity>();
        AsyncPageable<ClickStatsEntity> queryResult;

        if (string.IsNullOrEmpty(vanity))
        {
            queryResult = tblStats.QueryAsync<ClickStatsEntity>();
        }
        else
        {
            queryResult = tblStats.QueryAsync<ClickStatsEntity>(e => e.PartitionKey == vanity);
        }

        await foreach (var emp in queryResult.AsPages())
        {
            foreach (var item in emp.Values)
            {
                var clickDate = DateOnly.ParseExact(item.Datetime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

                Console.WriteLine($"Dates: {sDate.ToString()} <= {clickDate.ToString()} <= {eDate.ToString()}");

                if (clickDate >= sDate && clickDate <= eDate)
                {
                    Console.WriteLine($"ClickDate: {item.Datetime}");
                    lstUrlStats.Add(item);
                }
                    
            }
        }
        Console.WriteLine($"Count: {lstUrlStats.Count}");
        return lstUrlStats;
    }

    public async Task SaveClickStatsEntity(ClickStatsEntity newStats)
    {
        var result = await GetStatsTable().UpsertEntityAsync(newStats);
    }


    public async Task ImportUrlDataAsync(UrlDetails urlData)
    {
        try
        {
            var tblUrls = GetUrlsTable();
            var lstUrl = urlData.LstShortUrlEntity;

            foreach (var item in lstUrl)
            {
                await tblUrls.UpsertEntityAsync(item);
            }

            if (urlData.NextId != null)
            {
                await tblUrls.UpsertEntityAsync(urlData.NextId);
            }

        }
        catch (Exception ex)
        {
            var temp = ex.Message;
            throw;
        }
    }

    public async Task ImportClickStatsAsync(List<ClickStatsEntity> lstClickStats)
    {
        var tblStats = GetStatsTable();
        foreach (var item in lstClickStats)
        {
            await tblStats.UpsertEntityAsync(item);
        }
    }
}
