using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json;

class Server
{
    // Текущие данные сенсоров и состояние системы
    private static double Pressure = 0.0;
    private static double Flow = 0.0;
    private static int WaterLevel = 0;
    private static bool PumpActive = false;
    private static bool ValveOpen = true;

    // Пороговые значения для автоматического управления
    private static double MinPressure = 3.0;  // Минимальное давление для включения насоса
    private static double MaxPressure = 8.0;  // Максимальное давление для выключения насоса
    private static double MaxFlow = 80.0;     // Максимальная допустимая расход воды
    private static int MinWaterLevel = 10;    // Критический минимальный уровень воды

    // Состояние работы системы
    private static bool IsMonitoringActive = true;  // Флаг активности мониторинга
    private static bool IsManualMode = false;       // Режим управления: ручной или автоматический

    // Журнал событий
    private static List<string> Logs = new List<string>();

    static void Main()
    {
        // Инициализация HttpListener
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8080/"); // Сервер слушает порт 8080
        listener.Start();
        Console.WriteLine("Server is running at http://localhost:8080/");

        // Запускаем обработку запросов и мониторинг в отдельном потоке
        Task.Run(() => HandleRequests(listener));

        // Основной поток будет отвечать за управление
        while (true)
        {
            // Вывод меню управления в консоль
            DisplayMenu();
        }
    }

    // Обработка запросов от эмулятора и UI
    private static async Task HandleRequests(HttpListener listener)
    {
        while (true)
        {
            HttpListenerContext context = await listener.GetContextAsync();
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            if (request.Url.AbsolutePath == "/api/sensors" && request.HttpMethod == "POST")
            {
                HandleSensorData(request, response); // Обработка данных от эмулятора
            }
            else if (request.Url.AbsolutePath == "/api/sensors" && request.HttpMethod == "GET")
            {
                SendSensorData(response); // Отправка текущих данных
            }
            else if (request.Url.AbsolutePath == "/api/logs" && request.HttpMethod == "GET")
            {
                SendLogs(response); // Отправка журнала событий
            }
            else
            {
                response.StatusCode = 404; // Неверный запрос
                response.Close();
            }
        }
    }

    // Обработка POST-запроса с данными сенсоров
    private static void HandleSensorData(HttpListenerRequest request, HttpListenerResponse response)
    {
        using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
        {
            string body = reader.ReadToEnd();
            var sensorData = JsonConvert.DeserializeObject<SensorData>(body);

            // Обновление данных сенсоров
            Pressure = sensorData.Pressure;
            Flow = sensorData.Flow;
            WaterLevel = sensorData.WaterLevel;

            Logs.Add($"Received data: Pressure={Pressure}, Flow={Flow}, WaterLevel={WaterLevel}");

            if (IsMonitoringActive && !IsManualMode)
            {
                AutoControl(); // Автоматическое управление
            }

            // Ответ клиенту
            response.StatusCode = 200;
            response.Close();
        }
    }

    // Отправка текущих данных (GET /api/sensors)
    private static void SendSensorData(HttpListenerResponse response)
    {
        var data = new
        {
            Pressure,
            Flow,
            WaterLevel,
            PumpActive,
            ValveOpen,
            IsManualMode,
            IsMonitoringActive
        };

        string jsonResponse = JsonConvert.SerializeObject(data);
        byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);

        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.Close();
    }

    // Отправка журнала событий (GET /api/logs)
    private static void SendLogs(HttpListenerResponse response)
    {
        string logData = string.Join("\n", Logs);
        byte[] buffer = Encoding.UTF8.GetBytes(logData);

        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.Close();
    }

    // Автоматическое управление насосами и клапанами
    private static void AutoControl()
    {
        // Отображение текущих данных сенсоров
        Console.WriteLine($"Current Sensor Data: Pressure={Pressure}, Flow={Flow}, WaterLevel={WaterLevel}");

        // Управление насосом
        if (Pressure < MinPressure)
        {
            if (!PumpActive)
            {
                PumpActive = true;
                Logs.Add($"Pump activated due to low pressure. Pressure={Pressure}");
                Console.WriteLine($"Pump activated due to low pressure. Pressure={Pressure}");
            }
        }
        else if (Pressure > MaxPressure)
        {
            if (PumpActive)
            {
                PumpActive = false;
                Logs.Add($"Pump deactivated due to high pressure. Pressure={Pressure}");
                Console.WriteLine($"Pump deactivated due to high pressure. Pressure={Pressure}");
            }
        }

        // Управление клапаном
        if (Flow > MaxFlow)
        {
            if (ValveOpen)
            {
                ValveOpen = false;
                Logs.Add($"Valve closed due to high flow. Flow={Flow}");
                Console.WriteLine($"Valve closed due to high flow. Flow={Flow}");
            }
        }
        else
        {
            if (!ValveOpen)
            {
                ValveOpen = true;
                Logs.Add($"Valve opened for normal flow. Flow={Flow}");
                Console.WriteLine($"Valve opened for normal flow. Flow={Flow}");
            }
        }

        // Уведомление о низком уровне воды
        if (WaterLevel < MinWaterLevel)
        {
            Logs.Add($"ALERT: Water level critically low! WaterLevel={WaterLevel}");
            Console.WriteLine($"ALERT: Water level critically low! WaterLevel={WaterLevel}");
        }
    }

    // Отображение меню управления в консоли
    private static void DisplayMenu()
    {
        Console.WriteLine("\n=== Control Menu ===");
        Console.WriteLine("1. Toggle Manual Mode (current: " + (IsManualMode ? "Manual" : "Automatic") + ")");
        Console.WriteLine("2. Toggle Monitoring (current: " + (IsMonitoringActive ? "Active" : "Paused") + ")");
        Console.WriteLine("3. Update Threshold Values");
        Console.WriteLine("4. Manually control pump and valve");
        Console.WriteLine("Enter your choice:");

        string input = Console.ReadLine();
        switch (input)
        {
            case "1":
                IsManualMode = !IsManualMode;
                Console.WriteLine("Manual Mode " + (IsManualMode ? "Enabled" : "Disabled"));
                break;
            case "2":
                IsMonitoringActive = !IsMonitoringActive;
                Console.WriteLine("Monitoring " + (IsMonitoringActive ? "Resumed" : "Paused"));
                break;
            case "3":
                UpdateThresholds();
                break;
            case "4":
                ManuallyControlPumpAndValve();
                break;
            default:
                Console.WriteLine("Invalid option. Please try again.");
                break;
        }
    }

    // Обновление пороговых значений
    private static void UpdateThresholds()
    {
        Console.WriteLine("Enter new minimum pressure (current: " + MinPressure + "): ");
        MinPressure = double.Parse(Console.ReadLine());

        Console.WriteLine("Enter new maximum pressure (current: " + MaxPressure + "): ");
        MaxPressure = double.Parse(Console.ReadLine());

        Console.WriteLine("Enter new maximum flow (current: " + MaxFlow + "): ");
        MaxFlow = double.Parse(Console.ReadLine());

        Console.WriteLine("Enter new minimum water level (current: " + MinWaterLevel + "): ");
        MinWaterLevel = int.Parse(Console.ReadLine());

        Console.WriteLine("Thresholds updated successfully!");
    }

    // Ручное управление насосом и клапаном
    private static void ManuallyControlPumpAndValve()
    {
        Console.WriteLine("Enter the action for the pump (on/off): ");
        string pumpAction = Console.ReadLine().ToLower();
        if (pumpAction == "on")
        {
            PumpActive = true;
            Console.WriteLine("Pump is now ON.");
        }
        else if (pumpAction == "off")
        {
            PumpActive = false;
            Console.WriteLine("Pump is now OFF.");
        }
        else
        {
            Console.WriteLine("Invalid input. Pump not changed.");
        }

        Console.WriteLine("Enter the action for the valve (open/close): ");
        string valveAction = Console.ReadLine().ToLower();
        if (valveAction == "open")
        {
            ValveOpen = true;
            Console.WriteLine("Valve is now OPEN.");
        }
        else if (valveAction == "close")
        {
            ValveOpen = false;
            Console.WriteLine("Valve is now CLOSED.");
        }
        else
        {
            Console.WriteLine("Invalid input. Valve not changed.");
        }
    }
}

// Класс для данных сенсоров
public class SensorData
{
    public double Pressure { get; set; }
    public double Flow { get; set; }
    public int WaterLevel { get; set; }
}
