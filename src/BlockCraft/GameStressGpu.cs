using OpenTK.Graphics.OpenGL4;

namespace BlockCraft;

/// <summary>Czas GPU klatki w teście --stress (GL_TIME_ELAPSED, pierścień zapytań bez czekania na GPU).</summary>
public sealed partial class Game
{
    private int[] _gpuQ;
    private int _gpuHead, _gpuPending;
    private double _gpuSumMs; private int _gpuCount;

    private void StressGpuBegin()
    {
        if (_gpuQ == null) { _gpuQ = new int[8]; GL.GenQueries(_gpuQ.Length, _gpuQ); }
        // odbierz gotowe wyniki (najstarsze najpierw), żeby nie nadpisać trwającego zapytania
        while (_gpuPending > 0)
        {
            int q = _gpuQ[(_gpuHead - _gpuPending + _gpuQ.Length) % _gpuQ.Length];
            GL.GetQueryObject(q, GetQueryObjectParam.QueryResultAvailable, out int ready);
            if (ready == 0) { if (_gpuPending < _gpuQ.Length - 1) break; GL.GetQueryObject(q, GetQueryObjectParam.QueryResultAvailable, out ready); }
            GL.GetQueryObject(q, GetQueryObjectParam.QueryResult, out long ns);
            _gpuSumMs += ns / 1e6; _gpuCount++; _gpuPending--;
        }
        GL.BeginQuery(QueryTarget.TimeElapsed, _gpuQ[_gpuHead]);
    }

    private void StressGpuEnd()
    {
        GL.EndQuery(QueryTarget.TimeElapsed);
        _gpuHead = (_gpuHead + 1) % _gpuQ.Length; _gpuPending++;
    }

    private void StressGpuReset() { _gpuSumMs = 0; _gpuCount = 0; }
    private double StressGpuAvgMs => _gpuCount == 0 ? 0 : _gpuSumMs / _gpuCount;
}
