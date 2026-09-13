using System.Windows.Controls;
using Observables.Events.R3;
using R3;

Button button = new();
using IDisposable sub = button.RoutedEvents().Click.Subscribe(_ => { });

Console.WriteLine("Observables.Events.R3 routed-events consumer smoke OK");
