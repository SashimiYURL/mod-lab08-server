using System;
using System.Threading;
using System.Collections.Generic;
using ScottPlot;

namespace TPProj
{
    public class procEventArgs : EventArgs
    {
        public int id { get; set; }
    }

    struct PoolRecord
    {
        public Thread thread;
        public bool in_use;
    }

    class Server
    {
        public PoolRecord[] pool;
        private object threadLock = new object();
        public int requestCount = 0;
        public int processedCount = 0;
        public int rejectedCount = 0;
        private double serviceIntensity;

        public Server(int channels, double intensity)
        {
            pool = new PoolRecord[channels];
            serviceIntensity = intensity;
        }

        public void proc(object sender, procEventArgs e)
        {
            lock (threadLock)
            {
                requestCount++;
                for (int i = 0; i < pool.Length; i++)
                {
                    if (!pool[i].in_use)
                    {
                        pool[i].in_use = true;
                        pool[i].thread = new Thread(new ParameterizedThreadStart(Answer));
                        pool[i].thread.Start(e.id);
                        processedCount++;
                        return;
                    }
                }
                rejectedCount++;
            }
        }

        public void Answer(object arg)
        {
            int id = (int)arg;
            Thread.Sleep((int)(1000 / serviceIntensity));

            lock (threadLock)
            {
                for (int i = 0; i < pool.Length; i++)
                {
                    if (pool[i].thread == Thread.CurrentThread)
                    {
                        pool[i].in_use = false;
                        break;
                    }
                }
            }
        }
    }

    class Client
    {
        private Server server;
        private double requestIntensity;
        private Random rand = new Random();

        public Client(Server server, double intensity)
        {
            this.server = server;
            this.requestIntensity = intensity;
            this.request += server.proc;
        }

        public void send(int id)
        {
            double timeBetween = -Math.Log(1 - rand.NextDouble()) / requestIntensity;
            Thread.Sleep((int)(timeBetween * 1000));

            procEventArgs args = new procEventArgs();
            args.id = id;
            OnProc(args);
        }

        protected virtual void OnProc(procEventArgs e)
        {
            EventHandler<procEventArgs> handler = request;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public event EventHandler<procEventArgs> request;
    }

    class Program
    {
        static void Main()
        {
            const int n = 5; // Количество каналов
            const double μ = 0.5; // Интенсивность обслуживания
            const int totalRequests = 20;
            const int points = 10;

            var λ_values = new List<double>();
            var P0_theory = new List<double>();
            var Pn_theory = new List<double>();
            var Q_theory = new List<double>();
            var A_theory = new List<double>();
            var k_theory = new List<double>();

            var P0_practice = new List<double>();
            var Pn_practice = new List<double>();
            var Q_practice = new List<double>();
            var A_practice = new List<double>();
            var k_practice = new List<double>();

            for (int p = 0; p < points; p++)
            {
                double λ = 0.1 + (2.0 - 0.1) * p / (points - 1);
                λ_values.Add(λ);

                // Теоретические расчеты
                double ρ = λ / μ;
                double p0 = 1 / CalculateSum(ρ, n);
                double pn = (Math.Pow(ρ, n) / Factorial(n)) * p0;
                double Q = 1 - pn;
                double A = λ * Q;
                double k = A / μ;

                P0_theory.Add(p0);
                Pn_theory.Add(pn);
                Q_theory.Add(Q);
                A_theory.Add(A);
                k_theory.Add(k);

                // Практическое моделирование
                var server = new Server(n, μ);
                var client = new Client(server, λ);

                for (int id = 1; id <= totalRequests; id++)
                {
                    client.send(id);
                }

                while (server.processedCount + server.rejectedCount < totalRequests)
                {
                    Thread.Sleep(100);
                }

                P0_practice.Add(1 - (double)server.processedCount / server.requestCount);
                Pn_practice.Add((double)server.rejectedCount / server.requestCount);
                Q_practice.Add((double)server.processedCount / server.requestCount);
                A_practice.Add((double)server.processedCount / (server.requestCount / λ));
                k_practice.Add(((double)server.processedCount / (server.requestCount / λ)) / μ);
            }

            // Создание графиков
            Directory.CreateDirectory("result");
            CreatePlot(λ_values, P0_theory, P0_practice, "Вероятность простоя системы", "p-1.png");
            CreatePlot(λ_values, Pn_theory, Pn_practice, "Вероятность отказа системы", "p-2.png");
            CreatePlot(λ_values, Q_theory, Q_practice, "Относительная пропускная способность", "p-3.png");
            CreatePlot(λ_values, A_theory, A_practice, "Абсолютная пропускная способность", "p-4.png");
            CreatePlot(λ_values, k_theory, k_practice, "Среднее число занятых каналов", "p-5.png");

            Console.WriteLine("Моделирование завершено. Графики сохранены в папке results/");
            SaveDataToFile(λ_values, P0_theory, P0_practice, 
                         Pn_theory, Pn_practice,
                         Q_theory, Q_practice,
                         A_theory, A_practice,
                         k_theory, k_practice);

            Console.WriteLine("Моделирование завершено. Данные сохранены в data.txt");
        }

        static void SaveDataToFile(List<double> λ_values,
                                 List<double> P0_t, List<double> P0_p,
                                 List<double> Pn_t, List<double> Pn_p,
                                 List<double> Q_t, List<double> Q_p,
                                 List<double> A_t, List<double> A_p,
                                 List<double> k_t, List<double> k_p)
        {
            using (StreamWriter writer = new StreamWriter("data.txt"))
            {
                writer.WriteLine("λ\tP0 теория\tP0 практика\tPn теория\tPn практика\tQ теория\tQ практика\tA теория\tA практика\tk теория\tk практика");
                
                for (int i = 0; i < λ_values.Count; i++)
                {
                    writer.WriteLine($"{λ_values[i]:F2}\t{P0_t[i]:F4}\t{P0_p[i]:F4}\t" +
                                   $"{Pn_t[i]:F4}\t{Pn_p[i]:F4}\t" +
                                   $"{Q_t[i]:F4}\t{Q_p[i]:F4}\t" +
                                   $"{A_t[i]:F4}\t{A_p[i]:F4}\t" +
                                   $"{k_t[i]:F2}\t{k_p[i]:F2}");
                }
            }
        }

        static double CalculateSum(double ρ, int n)
        {
            double sum = 0;
            for (int k = 0; k <= n; k++)
            {
                sum += Math.Pow(ρ, k) / Factorial(k);
            }
            return sum;
        }

        static double Factorial(int n)
        {
            double result = 1;
            for (int i = 2; i <= n; i++) result *= i;
            return result;
        }

        static void CreatePlot(List<double> x, List<double> y1, List<double> y2, string title, string filename)
        {
            var plt = new ScottPlot.Plot(800, 600);
            plt.AddScatter(x.ToArray(), y1.ToArray(), label: "Теоретическая");
            plt.AddScatter(x.ToArray(), y2.ToArray(), label: "Экспериментальная");
            plt.Title(title);
            plt.XLabel("Интенсивность входящего потока (λ)");
            plt.YLabel(title);
            plt.Legend();
            plt.SaveFig($"results/{filename}");
        }
    }
}