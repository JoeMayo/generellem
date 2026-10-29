using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Data.Sqlite;

namespace Generellem.Services.Sqlite;

/// <summary>
/// Loads the bundled sqlite-vec extension and keeps a vec0 KNN index.
/// When the extension cannot be loaded, callers keep the brute-force scan.
/// </summary>
public sealed class SqliteVec
{
    public bool IsEnabled { get; private set; }

    public string? Detail { get; private set; }

    bool attempted;

    public void Ensure(SqliteConnection connection, int dimensions)
    {
        if (!attempted)
        {
            attempted = true;
            TryEnable(connection);
        }
        else if (IsEnabled)
        {
            TryLoad(connection, FindLibrary());
        }

        if (!IsEnabled)
            return;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"""
            CREATE VIRTUAL TABLE IF NOT EXISTS VectorChunkVec USING vec0(
                Embedding float[{dimensions}] distance_metric=cosine,
                +ChunkId text
            );
            """;
        try
        {
            command.ExecuteNonQuery();
        }
        catch (SqliteException ex)
        {
            IsEnabled = false;
            Detail = ex.Message;
        }
    }

    public void Upsert(SqliteConnection connection, string id, ReadOnlyMemory<float> embedding)
    {
        if (!IsEnabled)
            return;

        Delete(connection, [id]);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "INSERT INTO VectorChunkVec(Embedding, ChunkId) VALUES ($embedding, $id)";
        command.Parameters.AddWithValue("$embedding", ToJson(embedding));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void Delete(SqliteConnection connection, IReadOnlyList<string> ids)
    {
        if (!IsEnabled || ids.Count == 0)
            return;

        foreach (string id in ids)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM VectorChunkVec WHERE ChunkId = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
    }

    public void DeleteAll(SqliteConnection connection)
    {
        if (!IsEnabled)
            return;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM VectorChunkVec";
        command.ExecuteNonQuery();
    }

    public List<string> Search(SqliteConnection connection, ReadOnlyMemory<float> embedding, int limit)
    {
        if (!IsEnabled)
            return [];

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT ChunkId
            FROM VectorChunkVec
            WHERE Embedding MATCH $embedding AND k = $k
            ORDER BY distance
            """;
        command.Parameters.AddWithValue("$embedding", ToJson(embedding));
        command.Parameters.AddWithValue("$k", Math.Max(1, limit));

        List<string> ids = [];
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
                ids.Add(reader.GetString(0));
        }

        return ids;
    }

    void TryEnable(SqliteConnection connection)
    {
        string? path = FindLibrary();
        if (path is null)
        {
            Detail = "sqlite-vec library was not found. Vector search scans stored embeddings.";
            IsEnabled = false;
            return;
        }

        try
        {
            TryLoad(connection, path);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT vec_version()";
            Detail = "sqlite-vec " + command.ExecuteScalar();
            IsEnabled = true;
        }
        catch (Exception ex)
        {
            Detail = ex.Message;
            IsEnabled = false;
        }
    }

    static void TryLoad(SqliteConnection connection, string? path)
    {
        if (path is null)
            return;

        connection.EnableExtensions(true);
        connection.LoadExtension(path, "sqlite3_vec_init");
    }

    public static string? FindLibrary()
    {
        string? env = Environment.GetEnvironmentVariable("GENERELLEM_SQLITE_VEC");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        string file = LibraryFile();
        string rid = Rid();
        foreach (string start in StartDirectories())
        {
            DirectoryInfo? dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                string[] candidates =
                [
                    Path.Combine(dir.FullName, file),
                    Path.Combine(dir.FullName, "sqlite-vec", rid, file),
                    Path.Combine(dir.FullName, "Native", "sqlite-vec", rid, file),
                    Path.Combine(dir.FullName, "Generellem", "Native", "sqlite-vec", rid, file)
                ];

                foreach (string candidate in candidates)
                {
                    if (File.Exists(candidate))
                        return candidate;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    static IEnumerable<string> StartDirectories()
    {
        yield return AppContext.BaseDirectory;
        string? assemblyDir = Path.GetDirectoryName(typeof(SqliteVec).Assembly.Location);
        if (!string.IsNullOrEmpty(assemblyDir))
            yield return assemblyDir;
    }

    public static string LibraryFile() =>
        OperatingSystem.IsWindows() ? "vec0.dll"
        : OperatingSystem.IsMacOS() ? "vec0.dylib"
        : "vec0.so";

    public static string Rid()
    {
        bool arm64 = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        if (OperatingSystem.IsWindows())
            return "win-x64";
        if (OperatingSystem.IsMacOS())
            return arm64 ? "osx-arm64" : "osx-x64";
        return arm64 ? "linux-arm64" : "linux-x64";
    }

    public static string ToJson(ReadOnlyMemory<float> embedding)
    {
        ReadOnlySpan<float> span = embedding.Span;
        StringBuilder builder = new();
        builder.Append('[');
        for (int i = 0; i < span.Length; i++)
        {
            if (i > 0)
                builder.Append(',');
            builder.Append(span[i].ToString("R", CultureInfo.InvariantCulture));
        }

        builder.Append(']');
        return builder.ToString();
    }
}
