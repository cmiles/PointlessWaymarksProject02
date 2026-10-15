using Microsoft.Data.Sqlite;

namespace HistoricTempestsDataDownloader;

public class WeatherDatabase : IAsyncDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public WeatherDatabase(string databasePath)
    {
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        _connectionString = builder.ToString();
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync();

        // Enable WAL mode for performance and concurrent readers
        using (var walCmd = _connection.CreateCommand())
        {
            walCmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            await walCmd.ExecuteNonQueryAsync();
        }

        const string createTablesSql = """
            CREATE TABLE IF NOT EXISTS station_metadata (
                station_id INTEGER PRIMARY KEY,
                device_id INTEGER NOT NULL,
                station_name TEXT NOT NULL,
                timezone TEXT NOT NULL,
                timezone_offset_minutes INTEGER NOT NULL,
                latitude REAL NOT NULL,
                longitude REAL NOT NULL,
                elevation_meters REAL NOT NULL,
                created_epoch INTEGER NOT NULL,
                first_observation_date_local TEXT,
                last_observation_date_local TEXT,
                last_sync_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS daily_weather_summaries (
                date TEXT PRIMARY KEY,
                station_id INTEGER NOT NULL,
                device_id INTEGER NOT NULL,
                pressure_avg REAL,
                pressure_high REAL,
                pressure_low REAL,
                temperature_avg REAL,
                temperature_high REAL,
                temperature_low REAL,
                humidity_avg REAL,
                humidity_high REAL,
                humidity_low REAL,
                lux_avg REAL,
                lux_high REAL,
                lux_low REAL,
                uv_avg REAL,
                uv_high REAL,
                uv_low REAL,
                solar_radiation_avg REAL,
                solar_radiation_high REAL,
                solar_radiation_low REAL,
                wind_avg REAL,
                wind_gust REAL,
                wind_lull REAL,
                wind_direction_avg REAL,
                lightning_strike_count INTEGER,
                lightning_strike_avg_distance_km REAL,
                record_count INTEGER,
                battery_avg REAL,
                precipitation_accumulation_mm REAL,
                precipitation_final_mm REAL,
                precipitation_minutes REAL,
                precipitation_minutes_final REAL,
                precipitation_type INTEGER,
                created_utc TEXT NOT NULL,
                last_updated_utc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_summaries_device ON daily_weather_summaries(device_id);
            CREATE INDEX IF NOT EXISTS idx_summaries_station ON daily_weather_summaries(station_id);
            """;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = createTablesSql;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<HashSet<DateOnly>> GetExistingDatesAsync(DateOnly startDate, DateOnly endDate)
    {
        EnsureConnected();
        var existing = new HashSet<DateOnly>();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = """
            SELECT date FROM daily_weather_summaries
            WHERE date >= @start AND date <= @end
            ORDER BY date
            """;
        cmd.Parameters.AddWithValue("@start", startDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@end", endDate.ToString("yyyy-MM-dd"));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var dateStr = reader.GetString(0);
            if (DateOnly.TryParse(dateStr, out var d))
            {
                existing.Add(d);
            }
        }

        return existing;
    }

    public async Task<int> SaveDailySummariesAsync(IReadOnlyCollection<DailyWeatherSummary> summaries)
    {
        if (summaries.Count == 0) return 0;
        EnsureConnected();

        using var transaction = _connection!.BeginTransaction();
        const string upsertSql = """
            INSERT INTO daily_weather_summaries (
                date, station_id, device_id,
                pressure_avg, pressure_high, pressure_low,
                temperature_avg, temperature_high, temperature_low,
                humidity_avg, humidity_high, humidity_low,
                lux_avg, lux_high, lux_low,
                uv_avg, uv_high, uv_low,
                solar_radiation_avg, solar_radiation_high, solar_radiation_low,
                wind_avg, wind_gust, wind_lull, wind_direction_avg,
                lightning_strike_count, lightning_strike_avg_distance_km,
                record_count, battery_avg,
                precipitation_accumulation_mm, precipitation_final_mm,
                precipitation_minutes, precipitation_minutes_final, precipitation_type,
                created_utc, last_updated_utc
            ) VALUES (
                @date, @station_id, @device_id,
                @pressure_avg, @pressure_high, @pressure_low,
                @temperature_avg, @temperature_high, @temperature_low,
                @humidity_avg, @humidity_high, @humidity_low,
                @lux_avg, @lux_high, @lux_low,
                @uv_avg, @uv_high, @uv_low,
                @solar_radiation_avg, @solar_radiation_high, @solar_radiation_low,
                @wind_avg, @wind_gust, @wind_lull, @wind_direction_avg,
                @lightning_strike_count, @lightning_strike_avg_distance_km,
                @record_count, @battery_avg,
                @precipitation_accumulation_mm, @precipitation_final_mm,
                @precipitation_minutes, @precipitation_minutes_final, @precipitation_type,
                @created_utc, @last_updated_utc
            )
            ON CONFLICT(date) DO UPDATE SET
                station_id = excluded.station_id,
                device_id = excluded.device_id,
                pressure_avg = excluded.pressure_avg,
                pressure_high = excluded.pressure_high,
                pressure_low = excluded.pressure_low,
                temperature_avg = excluded.temperature_avg,
                temperature_high = excluded.temperature_high,
                temperature_low = excluded.temperature_low,
                humidity_avg = excluded.humidity_avg,
                humidity_high = excluded.humidity_high,
                humidity_low = excluded.humidity_low,
                lux_avg = excluded.lux_avg,
                lux_high = excluded.lux_high,
                lux_low = excluded.lux_low,
                uv_avg = excluded.uv_avg,
                uv_high = excluded.uv_high,
                uv_low = excluded.uv_low,
                solar_radiation_avg = excluded.solar_radiation_avg,
                solar_radiation_high = excluded.solar_radiation_high,
                solar_radiation_low = excluded.solar_radiation_low,
                wind_avg = excluded.wind_avg,
                wind_gust = excluded.wind_gust,
                wind_lull = excluded.wind_lull,
                wind_direction_avg = excluded.wind_direction_avg,
                lightning_strike_count = excluded.lightning_strike_count,
                lightning_strike_avg_distance_km = excluded.lightning_strike_avg_distance_km,
                record_count = excluded.record_count,
                battery_avg = excluded.battery_avg,
                precipitation_accumulation_mm = excluded.precipitation_accumulation_mm,
                precipitation_final_mm = excluded.precipitation_final_mm,
                precipitation_minutes = excluded.precipitation_minutes,
                precipitation_minutes_final = excluded.precipitation_minutes_final,
                precipitation_type = excluded.precipitation_type,
                last_updated_utc = excluded.last_updated_utc;
            """;

        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = upsertSql;

        var pDate = cmd.Parameters.Add("@date", SqliteType.Text);
        var pStation = cmd.Parameters.Add("@station_id", SqliteType.Integer);
        var pDevice = cmd.Parameters.Add("@device_id", SqliteType.Integer);
        var pPressAvg = cmd.Parameters.Add("@pressure_avg", SqliteType.Real);
        var pPressHigh = cmd.Parameters.Add("@pressure_high", SqliteType.Real);
        var pPressLow = cmd.Parameters.Add("@pressure_low", SqliteType.Real);
        var pTempAvg = cmd.Parameters.Add("@temperature_avg", SqliteType.Real);
        var pTempHigh = cmd.Parameters.Add("@temperature_high", SqliteType.Real);
        var pTempLow = cmd.Parameters.Add("@temperature_low", SqliteType.Real);
        var pHumAvg = cmd.Parameters.Add("@humidity_avg", SqliteType.Real);
        var pHumHigh = cmd.Parameters.Add("@humidity_high", SqliteType.Real);
        var pHumLow = cmd.Parameters.Add("@humidity_low", SqliteType.Real);
        var pLuxAvg = cmd.Parameters.Add("@lux_avg", SqliteType.Real);
        var pLuxHigh = cmd.Parameters.Add("@lux_high", SqliteType.Real);
        var pLuxLow = cmd.Parameters.Add("@lux_low", SqliteType.Real);
        var pUvAvg = cmd.Parameters.Add("@uv_avg", SqliteType.Real);
        var pUvHigh = cmd.Parameters.Add("@uv_high", SqliteType.Real);
        var pUvLow = cmd.Parameters.Add("@uv_low", SqliteType.Real);
        var pSolarAvg = cmd.Parameters.Add("@solar_radiation_avg", SqliteType.Real);
        var pSolarHigh = cmd.Parameters.Add("@solar_radiation_high", SqliteType.Real);
        var pSolarLow = cmd.Parameters.Add("@solar_radiation_low", SqliteType.Real);
        var pWindAvg = cmd.Parameters.Add("@wind_avg", SqliteType.Real);
        var pWindGust = cmd.Parameters.Add("@wind_gust", SqliteType.Real);
        var pWindLull = cmd.Parameters.Add("@wind_lull", SqliteType.Real);
        var pWindDir = cmd.Parameters.Add("@wind_direction_avg", SqliteType.Real);
        var pStrikes = cmd.Parameters.Add("@lightning_strike_count", SqliteType.Integer);
        var pStrikeDist = cmd.Parameters.Add("@lightning_strike_avg_distance_km", SqliteType.Real);
        var pRecords = cmd.Parameters.Add("@record_count", SqliteType.Integer);
        var pBattery = cmd.Parameters.Add("@battery_avg", SqliteType.Real);
        var pPrecipMm = cmd.Parameters.Add("@precipitation_accumulation_mm", SqliteType.Real);
        var pPrecipFinalMm = cmd.Parameters.Add("@precipitation_final_mm", SqliteType.Real);
        var pPrecipMin = cmd.Parameters.Add("@precipitation_minutes", SqliteType.Real);
        var pPrecipMinFinal = cmd.Parameters.Add("@precipitation_minutes_final", SqliteType.Real);
        var pPrecipType = cmd.Parameters.Add("@precipitation_type", SqliteType.Integer);
        var pCreated = cmd.Parameters.Add("@created_utc", SqliteType.Text);
        var pUpdated = cmd.Parameters.Add("@last_updated_utc", SqliteType.Text);

        int count = 0;
        foreach (var s in summaries)
        {
            pDate.Value = s.Date;
            pStation.Value = s.StationId;
            pDevice.Value = s.DeviceId;
            pPressAvg.Value = (object?)s.PressureAvg ?? DBNull.Value;
            pPressHigh.Value = (object?)s.PressureHigh ?? DBNull.Value;
            pPressLow.Value = (object?)s.PressureLow ?? DBNull.Value;
            pTempAvg.Value = (object?)s.TemperatureAvg ?? DBNull.Value;
            pTempHigh.Value = (object?)s.TemperatureHigh ?? DBNull.Value;
            pTempLow.Value = (object?)s.TemperatureLow ?? DBNull.Value;
            pHumAvg.Value = (object?)s.HumidityAvg ?? DBNull.Value;
            pHumHigh.Value = (object?)s.HumidityHigh ?? DBNull.Value;
            pHumLow.Value = (object?)s.HumidityLow ?? DBNull.Value;
            pLuxAvg.Value = (object?)s.LuxAvg ?? DBNull.Value;
            pLuxHigh.Value = (object?)s.LuxHigh ?? DBNull.Value;
            pLuxLow.Value = (object?)s.LuxLow ?? DBNull.Value;
            pUvAvg.Value = (object?)s.UvAvg ?? DBNull.Value;
            pUvHigh.Value = (object?)s.UvHigh ?? DBNull.Value;
            pUvLow.Value = (object?)s.UvLow ?? DBNull.Value;
            pSolarAvg.Value = (object?)s.SolarRadiationAvg ?? DBNull.Value;
            pSolarHigh.Value = (object?)s.SolarRadiationHigh ?? DBNull.Value;
            pSolarLow.Value = (object?)s.SolarRadiationLow ?? DBNull.Value;
            pWindAvg.Value = (object?)s.WindAvg ?? DBNull.Value;
            pWindGust.Value = (object?)s.WindGust ?? DBNull.Value;
            pWindLull.Value = (object?)s.WindLull ?? DBNull.Value;
            pWindDir.Value = (object?)s.WindDirectionAvg ?? DBNull.Value;
            pStrikes.Value = (object?)s.LightningStrikeCount ?? DBNull.Value;
            pStrikeDist.Value = (object?)s.LightningStrikeAvgDistanceKm ?? DBNull.Value;
            pRecords.Value = (object?)s.RecordCount ?? DBNull.Value;
            pBattery.Value = (object?)s.BatteryAvg ?? DBNull.Value;
            pPrecipMm.Value = (object?)s.PrecipitationAccumulationMm ?? DBNull.Value;
            pPrecipFinalMm.Value = (object?)s.PrecipitationFinalMm ?? DBNull.Value;
            pPrecipMin.Value = (object?)s.PrecipitationMinutes ?? DBNull.Value;
            pPrecipMinFinal.Value = (object?)s.PrecipitationMinutesFinal ?? DBNull.Value;
            pPrecipType.Value = (object?)s.PrecipitationType ?? DBNull.Value;
            pCreated.Value = s.CreatedUtc.ToString("o");
            pUpdated.Value = DateTime.UtcNow.ToString("o");

            await cmd.ExecuteNonQueryAsync();
            count++;
        }

        await transaction.CommitAsync();
        return count;
    }

    public async Task SaveStationMetadataAsync(StationMetadata meta)
    {
        EnsureConnected();
        const string sql = """
            INSERT INTO station_metadata (
                station_id, device_id, station_name, timezone, timezone_offset_minutes,
                latitude, longitude, elevation_meters, created_epoch,
                first_observation_date_local, last_observation_date_local, last_sync_utc
            ) VALUES (
                @station_id, @device_id, @station_name, @timezone, @timezone_offset_minutes,
                @latitude, @longitude, @elevation_meters, @created_epoch,
                @first_obs, @last_obs, @sync_utc
            )
            ON CONFLICT(station_id) DO UPDATE SET
                device_id = excluded.device_id,
                station_name = excluded.station_name,
                timezone = excluded.timezone,
                timezone_offset_minutes = excluded.timezone_offset_minutes,
                latitude = excluded.latitude,
                longitude = excluded.longitude,
                elevation_meters = excluded.elevation_meters,
                created_epoch = excluded.created_epoch,
                first_observation_date_local = COALESCE(excluded.first_observation_date_local, station_metadata.first_observation_date_local),
                last_observation_date_local = COALESCE(excluded.last_observation_date_local, station_metadata.last_observation_date_local),
                last_sync_utc = excluded.last_sync_utc;
            """;

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@station_id", meta.StationId);
        cmd.Parameters.AddWithValue("@device_id", meta.DeviceId);
        cmd.Parameters.AddWithValue("@station_name", meta.StationName);
        cmd.Parameters.AddWithValue("@timezone", meta.Timezone);
        cmd.Parameters.AddWithValue("@timezone_offset_minutes", meta.TimezoneOffsetMinutes);
        cmd.Parameters.AddWithValue("@latitude", meta.Latitude);
        cmd.Parameters.AddWithValue("@longitude", meta.Longitude);
        cmd.Parameters.AddWithValue("@elevation_meters", meta.ElevationMeters);
        cmd.Parameters.AddWithValue("@created_epoch", meta.CreatedEpoch);
        cmd.Parameters.AddWithValue("@first_obs", (object?)meta.FirstObservationDateLocal ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@last_obs", (object?)meta.LastObservationDateLocal ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sync_utc", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<StationMetadata?> GetStationMetadataAsync(long? stationId = null)
    {
        EnsureConnected();
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = stationId.HasValue
            ? "SELECT * FROM station_metadata WHERE station_id = @id LIMIT 1"
            : "SELECT * FROM station_metadata LIMIT 1";

        if (stationId.HasValue)
        {
            cmd.Parameters.AddWithValue("@id", stationId.Value);
        }

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new StationMetadata
        {
            StationId = reader.GetInt64(reader.GetOrdinal("station_id")),
            DeviceId = reader.GetInt64(reader.GetOrdinal("device_id")),
            StationName = reader.GetString(reader.GetOrdinal("station_name")),
            Timezone = reader.GetString(reader.GetOrdinal("timezone")),
            TimezoneOffsetMinutes = reader.GetInt32(reader.GetOrdinal("timezone_offset_minutes")),
            Latitude = reader.GetDouble(reader.GetOrdinal("latitude")),
            Longitude = reader.GetDouble(reader.GetOrdinal("longitude")),
            ElevationMeters = reader.GetDouble(reader.GetOrdinal("elevation_meters")),
            CreatedEpoch = reader.GetInt64(reader.GetOrdinal("created_epoch")),
            FirstObservationDateLocal = reader.IsDBNull(reader.GetOrdinal("first_observation_date_local")) ? "" : reader.GetString(reader.GetOrdinal("first_observation_date_local")),
            LastObservationDateLocal = reader.IsDBNull(reader.GetOrdinal("last_observation_date_local")) ? "" : reader.GetString(reader.GetOrdinal("last_observation_date_local")),
            LastSyncUtc = DateTime.TryParse(reader.GetString(reader.GetOrdinal("last_sync_utc")), out var dt) ? dt : DateTime.UtcNow
        };
    }

    public async Task<List<DailyWeatherSummary>> GetAllDailySummariesAsync()
    {
        EnsureConnected();
        var list = new List<DailyWeatherSummary>();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "SELECT * FROM daily_weather_summaries ORDER BY date ASC";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var s = new DailyWeatherSummary
            {
                Date = reader.GetString(reader.GetOrdinal("date")),
                StationId = reader.GetInt64(reader.GetOrdinal("station_id")),
                DeviceId = reader.GetInt64(reader.GetOrdinal("device_id")),
                PressureAvg = GetNullableDouble(reader, "pressure_avg"),
                PressureHigh = GetNullableDouble(reader, "pressure_high"),
                PressureLow = GetNullableDouble(reader, "pressure_low"),
                TemperatureAvg = GetNullableDouble(reader, "temperature_avg"),
                TemperatureHigh = GetNullableDouble(reader, "temperature_high"),
                TemperatureLow = GetNullableDouble(reader, "temperature_low"),
                HumidityAvg = GetNullableDouble(reader, "humidity_avg"),
                HumidityHigh = GetNullableDouble(reader, "humidity_high"),
                HumidityLow = GetNullableDouble(reader, "humidity_low"),
                LuxAvg = GetNullableDouble(reader, "lux_avg"),
                LuxHigh = GetNullableDouble(reader, "lux_high"),
                LuxLow = GetNullableDouble(reader, "lux_low"),
                UvAvg = GetNullableDouble(reader, "uv_avg"),
                UvHigh = GetNullableDouble(reader, "uv_high"),
                UvLow = GetNullableDouble(reader, "uv_low"),
                SolarRadiationAvg = GetNullableDouble(reader, "solar_radiation_avg"),
                SolarRadiationHigh = GetNullableDouble(reader, "solar_radiation_high"),
                SolarRadiationLow = GetNullableDouble(reader, "solar_radiation_low"),
                WindAvg = GetNullableDouble(reader, "wind_avg"),
                WindGust = GetNullableDouble(reader, "wind_gust"),
                WindLull = GetNullableDouble(reader, "wind_lull"),
                WindDirectionAvg = GetNullableDouble(reader, "wind_direction_avg"),
                LightningStrikeCount = GetNullableInt(reader, "lightning_strike_count"),
                LightningStrikeAvgDistanceKm = GetNullableDouble(reader, "lightning_strike_avg_distance_km"),
                RecordCount = GetNullableInt(reader, "record_count"),
                BatteryAvg = GetNullableDouble(reader, "battery_avg"),
                PrecipitationAccumulationMm = GetNullableDouble(reader, "precipitation_accumulation_mm"),
                PrecipitationFinalMm = GetNullableDouble(reader, "precipitation_final_mm"),
                PrecipitationMinutes = GetNullableDouble(reader, "precipitation_minutes"),
                PrecipitationMinutesFinal = GetNullableDouble(reader, "precipitation_minutes_final"),
                PrecipitationType = GetNullableInt(reader, "precipitation_type"),
                CreatedUtc = DateTime.TryParse(reader.GetString(reader.GetOrdinal("created_utc")), out var c) ? c : DateTime.UtcNow,
                LastUpdatedUtc = DateTime.TryParse(reader.GetString(reader.GetOrdinal("last_updated_utc")), out var u) ? u : DateTime.UtcNow
            };
            list.Add(s);
        }

        return list;
    }

    public async Task<int> GetCountAsync()
    {
        EnsureConnected();
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM daily_weather_summaries";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private static double? GetNullableDouble(SqliteDataReader reader, string column)
    {
        int ord = reader.GetOrdinal(column);
        return reader.IsDBNull(ord) ? null : reader.GetDouble(ord);
    }

    private static int? GetNullableInt(SqliteDataReader reader, string column)
    {
        int ord = reader.GetOrdinal(column);
        return reader.IsDBNull(ord) ? null : reader.GetInt32(ord);
    }

    private void EnsureConnected()
    {
        if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
        {
            throw new InvalidOperationException("Database connection is not open. Call InitializeAsync() first.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
