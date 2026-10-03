using Google.OrTools.ConstraintSolver;
using Google.Protobuf.WellKnownTypes;
using HealthApp.Application.Abstractions;

namespace HealthApp.Infrastructure;

public sealed class OrToolsMultiDriverRoutePlanningService : IMultiDriverRoutePlanningService
{
    public Task<MultiDriverRoutePlan> OptimizeAsync(
        IReadOnlyList<Guid> driverIds,
        IReadOnlyList<RouteOptimizationStop> points,
        RouteTravelMatrix matrix,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (driverIds.Count == 0 || points.Count <= 1)
            return Task.FromResult(new MultiDriverRoutePlan([]));

        if (matrix.DurationSeconds.GetLength(0) != points.Count ||
            matrix.DurationSeconds.GetLength(1) != points.Count)
            throw new ArgumentException("Routing matrix does not match the delivery locations.", nameof(matrix));

        var manager = new RoutingIndexManager(points.Count, driverIds.Count, 0);
        var routing = new RoutingModel(manager);

        var transitCallbackIndex = routing.RegisterTransitCallback((long fromIndex, long toIndex) =>
        {
            var fromNode = manager.IndexToNode(fromIndex);
            var toNode = manager.IndexToNode(toIndex);
            return matrix.DurationSeconds[fromNode, toNode];
        });

        routing.SetArcCostEvaluatorOfAllVehicles(transitCallbackIndex);
        routing.AddDimension(transitCallbackIndex, 0, 24L * 60L * 60L, true, "TravelTime");

        var travelTime = routing.GetDimensionOrDie("TravelTime");
        travelTime.SetGlobalSpanCostCoefficient(100);

        var searchParameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();
        searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;
        searchParameters.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch;
        searchParameters.TimeLimit = new Duration { Seconds = 8 };

        var solution = routing.SolveWithParameters(searchParameters)
            ?? throw new InvalidOperationException("The multi-driver route optimizer could not find a solution.");

        var routes = new List<DriverRouteAssignment>();

        for (var vehicle = 0; vehicle < driverIds.Count; vehicle++)
        {
            if (!routing.IsVehicleUsed(solution, vehicle))
                continue;

            var index = routing.Start(vehicle);
            var stopIds = new List<Guid>();

            while (!routing.IsEnd(index))
            {
                var node = manager.IndexToNode(index);
                if (node > 0)
                    stopIds.Add(points[node].Id);

                index = solution.Value(routing.NextVar(index));
            }

            if (stopIds.Count > 0)
                routes.Add(new DriverRouteAssignment(driverIds[vehicle], stopIds));
        }

        var assigned = routes.SelectMany(x => x.StopIds).ToHashSet();
        if (assigned.Count != points.Count - 1)
            throw new InvalidOperationException("The optimizer did not assign every delivery stop.");

        return Task.FromResult(new MultiDriverRoutePlan(routes));
    }
}
