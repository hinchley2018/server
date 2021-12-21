using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace GraphQL
{
    internal static class MyLogging
    {
        public static void MyLog<T>(this T obj, string messsage) where T : class
            => MyLog2<T>.LogMessage(obj, messsage);

        private class MyLog2<T>
            where T : class
        {
            private static int _counter = 0;
            private static List<(WeakReference<T>, int)> _weakReferences = new();

            public static void LogMessage(T obj, string value)
            {
                lock (_weakReferences)
                {
                    string stringToLog;
                    for (var i = _weakReferences.Count - 1; i >= 0; i--)
                    {
                        var weakReference = _weakReferences[i];
                        if (!weakReference.Item1.TryGetTarget(out var weak))
                            _weakReferences.RemoveAt(i);
                        else if (object.ReferenceEquals(weak, obj))
                        {
                            stringToLog = $"{obj.GetType().Name} #{weakReference.Item2}: {value}";
                            goto LogIt;
                        }
                    }
                    var newId = _counter++;
                    _weakReferences.Add((new WeakReference<T>(obj, false), newId));
                    stringToLog = $"{obj.GetType().Name} #{newId}: {value}";

                LogIt:
                    stringToLog = DateTime.UtcNow.ToString("o") + " " + stringToLog;
                    try
                    {
                        using var sw = new System.IO.StreamWriter("testwebserver.txt", true);
                        sw.WriteLine(stringToLog);
                        sw.Flush();
                    }
                    catch { }
                }
            }
        }
    }
}
