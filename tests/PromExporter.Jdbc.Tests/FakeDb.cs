using System.Collections;
using System.Data;
using System.Data.Common;

namespace PromExporter.Jdbc.Tests;

public sealed record FakeResult(string[] Columns, Type[] Types, List<object?[]> Rows);

public sealed class FakeOutcome
{
    public FakeResult? Result { get; init; }
    public Exception? Error { get; init; }
    public static FakeOutcome NonQuery() => new();
    public static FakeOutcome Of(FakeResult r) => new() { Result = r };
    public static FakeOutcome Fail(Exception e) => new() { Error = e };
}

/// <summary>Scripted in-memory ADO.NET stack: key is the exact command text.</summary>
public sealed class FakeDb : IDbConnectionProvider
{
    public FakeDbConnection Connection { get; } = new();
    public bool FailConnect { get; set; }

    public Task<DbConnection> GetOpenConnectionAsync(CancellationToken ct = default)
        => FailConnect ? Task.FromException<DbConnection>(new InvalidOperationException("connect refused"))
                       : Task.FromResult<DbConnection>(Connection);
}

public sealed class FakeDbConnection : DbConnection
{
    public Dictionary<string, FakeOutcome> Script { get; } = [];
    public List<string> Executed { get; } = [];

    public override ConnectionState State => ConnectionState.Open;
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string ConnectionString { get; set; } = "";
    public override string Database => "fake";
    public override string DataSource => "fake";
    public override string ServerVersion => "1.0";
    public override void ChangeDatabase(string databaseName) { }
    public override void Close() { }
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
    public override void Open() { }
    protected override DbCommand CreateDbCommand() => new FakeCommand(this);
}

public sealed class FakeCommand(FakeDbConnection conn) : DbCommand
{
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; } = conn;
    protected override DbParameterCollection DbParameterCollection { get; } = null!;
    protected override DbTransaction? DbTransaction { get; set; }
    public override void Cancel() { }
    public override void Prepare() { }
    protected override DbParameter CreateDbParameter() => throw new NotSupportedException();
    public override int ExecuteNonQuery() => Run();
    public override object? ExecuteScalar() => Run();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        Run();
        var outcome = conn.Script.TryGetValue(CommandText, out var o) ? o : FakeOutcome.NonQuery();
        return new FakeReader(outcome.Result ?? new FakeResult([], [], []));
    }

    private int Run()
    {
        conn.Executed.Add(CommandText);
        var outcome = conn.Script.TryGetValue(CommandText, out var o) ? o : FakeOutcome.NonQuery();
        if (outcome.Error != null) throw outcome.Error;
        return 0;
    }
}

public sealed class FakeReader(FakeResult result) : DbDataReader
{
    private int _pos = -1;
    private object? Cell(int i) => result.Rows[_pos][i];

    public override int FieldCount => result.Columns.Length;
    public override int Depth => 0;
    public override bool HasRows => result.Rows.Count > 0;
    public override bool IsClosed => false;
    public override int RecordsAffected => -1;
    public override int VisibleFieldCount => FieldCount;
    public override object this[int ordinal] => Cell(ordinal)!;
    public override object this[string name] => Cell(GetOrdinal(name))!;
    public override bool Read() => ++_pos < result.Rows.Count;
    public override bool NextResult() => false;
    public override void Close() { }
    public override bool IsDBNull(int i) => Cell(i) is null;
    public override Task<bool> IsDBNullAsync(int i, CancellationToken cancellationToken) => Task.FromResult(IsDBNull(i));
    public override string GetName(int i) => result.Columns[i];
    public override string GetDataTypeName(int i) => result.Types[i].Name;
    public override Type GetFieldType(int i) => result.Types[i];
    public override object GetValue(int i) => Cell(i)!;
    public override T GetFieldValue<T>(int i) => (T)Cell(i)!;
    public override Task<T> GetFieldValueAsync<T>(int i, CancellationToken cancellationToken = default)
        => Task.FromResult(GetFieldValue<T>(i));
    public override IEnumerator GetEnumerator() => result.Rows.GetEnumerator();
    public override int GetOrdinal(string name) => Array.IndexOf(result.Columns, name);
    public override int GetValues(object?[] values)
    {
        var n = Math.Min(values.Length, FieldCount);
        Array.Copy(result.Rows[_pos], values, n);
        return n;
    }

    public override bool GetBoolean(int i) => (bool)Cell(i)!;
    public override byte GetByte(int i) => (byte)Cell(i)!;
    public override char GetChar(int i) => (char)Cell(i)!;
    public override DateTime GetDateTime(int i) => (DateTime)Cell(i)!;
    public override decimal GetDecimal(int i) => (decimal)Cell(i)!;
    public override double GetDouble(int i) => Convert.ToDouble(Cell(i)!);
    public override float GetFloat(int i) => Convert.ToSingle(Cell(i)!);
    public override Guid GetGuid(int i) => (Guid)Cell(i)!;
    public override short GetInt16(int i) => Convert.ToInt16(Cell(i)!);
    public override int GetInt32(int i) => Convert.ToInt32(Cell(i)!);
    public override long GetInt64(int i) => Convert.ToInt64(Cell(i)!);
    public override string GetString(int i) => (string)Cell(i)!;
    public override long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
    public override long GetChars(int i, long fieldOffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
}
