using System.Diagnostics;

namespace DiskScope.Services;

public class ProcessResourceSample
{
    public double CpuPercentage { get; init; }
    public double RamMegabytes { get; init; }
    public DateTime Timestamp { get; init; }
    public bool IsValid { get; init; }
}

public class ProcessMonitorService : IDisposable
{
    private readonly Process _process;
    private readonly int _processorCount;
    private readonly int _capacity;
    private readonly System.Threading.Timer _timer;

    private readonly double[] _cpuRing;
    private readonly double[] _ramRing;
    private int _cpuCount;
    private int _ramCount;
    private int _cpuHead;
    private int _ramHead;

    private readonly object _dataLock = new();

    private TimeSpan _lastCpuTime;
    private DateTime _lastSampleTime;
    private bool _hasFirstSample;
    private bool _disposed;

    private double _currentRamMaxCeiling = 256.0;

    public event Action<ProcessResourceSample>? SampleTaken;

    public double CurrentCpu { get; private set; }
    public double CurrentRamMb { get; private set; }
    public bool LastSampleValid { get; private set; }

    public ProcessMonitorService(int historyCapacity = 60, int intervalMs = 1000)
    {
        _capacity = Math.Max(historyCapacity, 10);
        _cpuRing = new double[_capacity];
        _ramRing = new double[_capacity];

        _process = Process.GetCurrentProcess();
        _processorCount = Math.Max(Environment.ProcessorCount, 1);

        try
        {
            _lastCpuTime = _process.TotalProcessorTime;
            _lastSampleTime = DateTime.UtcNow;
            _hasFirstSample = true;
        }
        catch
        {
            _hasFirstSample = false;
        }

        _timer = new System.Threading.Timer(OnTimerTick, null, intervalMs, intervalMs);
    }

    public void OnTimerTick(object? state)
    {
        if (_disposed) return;
        var sample = CaptureSample();
        SampleTaken?.Invoke(sample);
    }

    public ProcessResourceSample CaptureSample()
    {
        try
        {
            _process.Refresh();

            var currentCpuTime = _process.TotalProcessorTime;
            var now = DateTime.UtcNow;

            double cpuPercent = 0.0;
            if (_hasFirstSample)
            {
                double elapsedCpuMs = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
                double elapsedRealMs = (now - _lastSampleTime).TotalMilliseconds;

                if (elapsedRealMs > 50 && _processorCount > 0)
                {
                    cpuPercent = (elapsedCpuMs / (elapsedRealMs * _processorCount)) * 100.0;
                }
            }

            cpuPercent = Math.Clamp(cpuPercent, 0.0, 100.0);
            _lastCpuTime = currentCpuTime;
            _lastSampleTime = now;
            _hasFirstSample = true;

            long workingSet = _process.WorkingSet64;
            double ramMb = workingSet / (1024.0 * 1024.0);
            if (ramMb < 0) ramMb = 0;

            lock (_dataLock)
            {
                PushValue(_cpuRing, ref _cpuCount, ref _cpuHead, cpuPercent);
                PushValue(_ramRing, ref _ramCount, ref _ramHead, ramMb);
                UpdateRamCeiling(ramMb);
            }

            CurrentCpu = cpuPercent;
            CurrentRamMb = ramMb;
            LastSampleValid = true;

            return new ProcessResourceSample
            {
                CpuPercentage = cpuPercent,
                RamMegabytes = ramMb,
                Timestamp = now,
                IsValid = true
            };
        }
        catch
        {
            LastSampleValid = false;
            return new ProcessResourceSample
            {
                CpuPercentage = 0,
                RamMegabytes = 0,
                Timestamp = DateTime.UtcNow,
                IsValid = false
            };
        }
    }

    private void PushValue(double[] ring, ref int count, ref int head, double value)
    {
        if (count < ring.Length)
        {
            ring[count] = value;
            count++;
        }
        else
        {
            ring[head] = value;
            head = (head + 1) % ring.Length;
        }
    }

    public double[] GetCpuHistory()
    {
        lock (_dataLock)
        {
            return ExtractOrdered(_cpuRing, _cpuCount, _cpuHead);
        }
    }

    public double[] GetRamHistory()
    {
        lock (_dataLock)
        {
            return ExtractOrdered(_ramRing, _ramCount, _ramHead);
        }
    }

    private static double[] ExtractOrdered(double[] ring, int count, int head)
    {
        if (count == 0) return Array.Empty<double>();
        double[] result = new double[count];
        if (count < ring.Length)
        {
            Array.Copy(ring, result, count);
        }
        else
        {
            int part1 = ring.Length - head;
            Array.Copy(ring, head, result, 0, part1);
            if (head > 0)
            {
                Array.Copy(ring, 0, result, part1, head);
            }
        }
        return result;
    }

    public double GetCpuPeak()
    {
        lock (_dataLock)
        {
            if (_cpuCount == 0) return 0;
            double max = 0;
            for (int i = 0; i < _cpuCount; i++)
            {
                if (_cpuRing[i] > max) max = _cpuRing[i];
            }
            return max;
        }
    }

    public double DynamicRamCeiling
    {
        get
        {
            lock (_dataLock)
            {
                return _currentRamMaxCeiling;
            }
        }
    }

    private void UpdateRamCeiling(double currentRamMb)
    {
        // 25% headroom over recent maximum
        double observedMax = currentRamMb;
        for (int i = 0; i < _ramCount; i++)
        {
            if (_ramRing[i] > observedMax) observedMax = _ramRing[i];
        }

        double needed = observedMax * 1.25;
        if (needed < 128.0) needed = 128.0;

        // Steps in 64MB increments up to 512MB, 128MB thereafter
        double step = needed > 512.0 ? 128.0 : 64.0;
        double roundedCeiling = Math.Ceiling(needed / step) * step;

        if (roundedCeiling > _currentRamMaxCeiling)
        {
            _currentRamMaxCeiling = roundedCeiling;
        }
        else if (needed < _currentRamMaxCeiling * 0.55 && roundedCeiling < _currentRamMaxCeiling)
        {
            _currentRamMaxCeiling = roundedCeiling;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
    }
}
