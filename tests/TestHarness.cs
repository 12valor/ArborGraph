using System.Diagnostics;
using System.IO;

namespace DiskScope.Tests;

public enum TestOutcome
{
    Pass,
    Fail,
    Skipped,
    Blocked
}

public record TestCase(
    string TestId,
    string FeatureId,
    string Title,
    string Priority,
    string Category,
    string ProductionClass,
    string? AuditRiskNote,
    Func<TestExecutionContext, Task> ExecuteAsync
);

public class TestResult
{
    public required TestCase Case { get; init; }
    public TestOutcome Outcome { get; set; } = TestOutcome.Skipped;
    public TimeSpan Elapsed { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
}

public class TestExecutionContext : IDisposable
{
    private readonly List<string> _tempDirectories = new();

    public string CreateTempDirectory(string prefix = "test_sub")
    {
        string dir = TestDataGenerator.CreateIsolatedDirectory(prefix);
        _tempDirectories.Add(dir);
        return dir;
    }

    public string CreateTempDatabasePath(string name = "test_db.sqlite")
    {
        string dir = CreateTempDirectory("db_store");
        return Path.Combine(dir, name);
    }

    public void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new TestAssertionException(message);
        }
    }

    public void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new TestAssertionException($"{message} | Expected: [{expected}], Actual: [{actual}]");
        }
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirectories)
        {
            TestDataGenerator.SafeCleanup(dir);
        }
        _tempDirectories.Clear();
    }
}

public class TestAssertionException : Exception
{
    public TestAssertionException(string message) : base(message) { }
}

public class TestRunner
{
    private readonly List<TestCase> _testCases = new();

    public void Register(TestCase testCase)
    {
        _testCases.Add(testCase);
    }

    public IReadOnlyList<TestCase> RegisteredCases => _testCases;

    public async Task<List<TestResult>> RunAsync(
        Func<TestCase, bool>? filter = null,
        Action<TestResult>? onTestCompleted = null)
    {
        var results = new List<TestResult>();
        var casesToRun = filter != null ? _testCases.Where(filter).ToList() : _testCases;

        foreach (var tc in casesToRun)
        {
            using var context = new TestExecutionContext();
            var sw = Stopwatch.StartNew();
            var result = new TestResult { Case = tc };

            try
            {
                await tc.ExecuteAsync(context);
                sw.Stop();
                result.Outcome = TestOutcome.Pass;
                result.Elapsed = sw.Elapsed;
            }
            catch (TestAssertionException ex)
            {
                sw.Stop();
                result.Outcome = TestOutcome.Fail;
                result.Elapsed = sw.Elapsed;
                result.ErrorMessage = ex.Message;
                result.StackTrace = ex.StackTrace;
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Outcome = TestOutcome.Fail;
                result.Elapsed = sw.Elapsed;
                result.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
                result.StackTrace = ex.StackTrace;
            }

            results.Add(result);
            onTestCompleted?.Invoke(result);
        }

        return results;
    }
}
