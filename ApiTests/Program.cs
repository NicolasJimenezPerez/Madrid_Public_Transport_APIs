using TransportAPIs;

namespace ApiTests
{
    class Program
    {
        static void Main()
        {
            EmtApi emtApi = new EmtApi();
    
            EmtApi.BusStop[] busStops = new EmtApi.BusStop[]
            {
                emtApi.GetBusStop(1058),
                emtApi.GetBusStop(1059),
            };
        }
    }
}
