using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace GraphQL.Server.TestWebServer
{
    public class TestService
    {
        private static int _staticId = 0;
        private readonly int _id = System.Threading.Interlocked.Increment(ref _staticId);

        public TestService()
        {
            Debug.WriteLine($"Create new TestService {_id}");
            Update();
        }

        public async void Update()
        {
            try
            {
                while (true)
                {
                    _testStream.OnNext(new TestItem() { Value = new Random().Next(1000) });
                    await Task.Delay(TimeSpan.FromSeconds(120));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Caught error in TestService {_id} Update: {ex}");
            }
            finally
            {
                Debug.WriteLine($"TestService {_id} terminated Update");
            }
        }

        public IObservable<TestItem> GetEvents()
        {
            return _testStream.AsObservable();
        }

        private readonly ISubject<TestItem> _testStream = new ReplaySubject<TestItem>(1);
    }
}
