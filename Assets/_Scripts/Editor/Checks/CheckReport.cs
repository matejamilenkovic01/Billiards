using System.Text;
using UnityEngine;

// Tiny assertion collector for the eval-driven checks: every check returns a readable report.
public sealed class CheckReport
{
    private readonly string _name;
    private readonly StringBuilder _lines = new();
    private int _passed;
    private int _failed;

    public CheckReport(string name) => _name = name;

    public bool AllPassed => _failed == 0;

    public bool Check(bool condition, string description)
    {
        if (condition)
            _passed++;
        else
        {
            _failed++;
            _lines.AppendLine("FAIL: " + description);
        }
        return condition;
    }

    public void Info(string line) => _lines.AppendLine(line);

    public override string ToString()
    {
        string header = $"{_name}: {(AllPassed ? "PASS" : "FAIL")} ({_passed} passed, {_failed} failed)";
        string body = _lines.ToString().TrimEnd();
        string result = body.Length > 0 ? header + "\n" + body : header;
        if (AllPassed)
            Debug.Log(result);
        else
            Debug.LogWarning(result);
        return result;
    }
}
