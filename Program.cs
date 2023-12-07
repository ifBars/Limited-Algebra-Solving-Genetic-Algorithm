using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using NCalc;

class Program
{
    static double expectedSolution;
    static Random rand = new Random(); // Moved outside Main
    static int mutations = 0;
    static int crossovers = 0;

    static void Main()
    {
        double mutationRate = 0.3;
        double crossoverRate = 0.8;

        Console.WriteLine("Enter the algebraic equation to be solved (e.g., 50 * ((3 * [x]) − 2) + [x] / 3 = 200:");
        string equation = Console.ReadLine();

        string[] sides = equation.Split('=');
        if (sides.Length != 2)
        {
            Console.WriteLine("Invalid equation format.");
            return;
        }

        if (!double.TryParse(sides[1], out expectedSolution))
        {
            Console.WriteLine("Invalid expected solution format.");
            return;
        }

        Console.WriteLine("Enter the desired population size per generation:");
        int populationSize = int.Parse(Console.ReadLine());

        Console.WriteLine("Enter the desired number of generations:");
        int generations = int.Parse(Console.ReadLine());

        List<Solution> totalPopulation = new List<Solution>();
        List<Solution> population = InitializePopulation(populationSize);

        int counter = 0;
        object lockObject = new object();  // Create a lock object

        Parallel.For(0, generations, generation =>
        {
            Solution bestSolution = null;

            bestSolution = Train(population, generation, equation);

            // Check if the population is empty and repopulate if needed
            if (population.Count == 0)
            {
                // Console.WriteLine($"Repopulating the population for generation {generation + 1}.");
                population = InitializePopulation(populationSize);

                bestSolution = Train(population, generation, equation);
            }

            List<Solution> selected = Select(population);
            List<Solution> newPopulation = Crossover(selected, crossoverRate);
            Mutate(newPopulation, mutationRate);

            // Use lock to ensure that only one thread can modify the lists at a time
            lock (lockObject)
            {
                population = newPopulation;
                totalPopulation.AddRange(population);
            }

            counter++;
            Console.Clear();
            Console.WriteLine("Generations Complete: " + counter + "/" + generations);
        });

        // Find and display the best solution in the final population
        Solution finalBestSolution = GetBestSolution(totalPopulation);
        if (finalBestSolution != null)
        {
            int totalSize = populationSize * generations;
            Console.Clear();
            Console.WriteLine($"Total Population Size: {totalSize}");
            Console.WriteLine($"Total Living Population Size: {totalPopulation.Count}");
            Console.WriteLine($"Generation Population Size: {populationSize}");
            Console.WriteLine($"Total Generations: {generations}");
            Console.WriteLine($"Total Mutations: {mutations}");
            Console.WriteLine($"Total Crossovers: {crossovers}");
            Console.WriteLine($"Final Best Solution Found: {finalBestSolution}");
            Console.WriteLine($"Best Solution's Equation Result: {finalBestSolution.Result}");
        }
        else
        {
            Console.WriteLine("No solutions found in the final population.");
        }
    }

    static Solution Train(List<Solution> population, int generation, string equation)
    {
        EvaluateFitness(population, equation);

        Solution bestSolution = GetBestSolution(population);
        if (bestSolution != null)
        {
            Console.WriteLine($"Generation {generation + 1}: Best solution - {bestSolution}");
        }
        else
        {
            // Console.WriteLine($"Generation {generation + 1}: No solutions found in the current generation.");
        }

        return bestSolution;
    }

    static List<Solution> InitializePopulation(int size)
    {
        List<Solution> population = new List<Solution>();

        for (int i = 0; i < size; i++)
        {
            Solution solution = new Solution(rand.NextDouble() * 20 - 10); // Random initialization between -10 and 10
            population.Add(solution);
        }

        return population;
    }

    static string ConvertToExpression(string equation)
    {
        // Remove the equal sign and everything after it
        int equalSignIndex = equation.IndexOf('=');
        if (equalSignIndex >= 0)
        {
            equation = equation.Substring(0, equalSignIndex);
        }

        // Replace the variable with its value in the equation
        return equation;
    }

    static void EvaluateFitness(List<Solution> population, string equation)
    {
        Parallel.ForEach(population, solution =>
        {
            try
            {
                string modifiedEquation = ConvertToExpression(equation);

                // Evaluate the modified equation using NCalc
                Expression expression = new Expression(modifiedEquation);
                expression.Parameters["x"] = solution.Parameter;
                double resultValue = Convert.ToDouble(expression.Evaluate());

                solution.Result = resultValue;

                // Print the modified equation and result for debugging
                // Console.WriteLine($"Parameter: {solution.Parameter}, Result: {expression.Evaluate()}");

                // Calculate and assign fitness
                double difference = Math.Abs(resultValue - expectedSolution);
                double normalizedDifference = 1.0 - (difference);
                double accuracy = Math.Max(0.0, normalizedDifference); // Ensure accuracy is not less than 0.0
                solution.Fitness = accuracy;

                // Print the fitness value for debugging
                Console.WriteLine($"Parameter: {solution.Parameter}, Fitness: {solution.Fitness}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error evaluating the equation: {ex.Message}");
            }
        });
    }

    static List<Solution> Select(List<Solution> population)
    {
        // Select the top half of the population based on fitness
        return population.OrderByDescending(s => s.Fitness).Take(population.Count / 2).ToList();
    }

    static List<Solution> Crossover(List<Solution> selected, double crossoverRate)
    {
        List<Solution> newPopulation = new List<Solution>();

        foreach (Solution parent1 in selected)
        {
            Solution parent2 = selected[rand.Next(selected.Count)];

            // Perform crossover with a probability of crossoverRate
            if (rand.NextDouble() < crossoverRate)
            {
                // Combine parameters of two parents to create a child
                Solution child = new Solution((parent1.Parameter + parent2.Parameter) / 2);
                newPopulation.Add(child);
                crossovers++;
            }
            else
            {
                // If no crossover, add the parents directly to the new population
                newPopulation.Add(parent1);
                newPopulation.Add(parent2);
            }
        }

        return newPopulation;
    }

    static void Mutate(List<Solution> population, double mutationRate)
    {
        foreach (Solution solution in population)
        {
            // Introduce random mutation to the parameter with a probability of mutationRate
            if (rand.NextDouble() < mutationRate)
            {
                solution.Parameter += rand.NextDouble() * 2 - 1; // Mutation between -1 and 1
                mutations++;
            }
        }
    }

    static Solution GetBestSolution(List<Solution> population)
    {
        return population.OrderByDescending(s => s.Fitness).FirstOrDefault();
    }
}

class Solution
{
    public double Parameter { get; set; }
    public double Fitness { get; set; }

    public double Result { get; set; }

    public Solution(double parameter)
    {
        Parameter = parameter;
    }

    public override string ToString()
    {
        return $"Parameter: {Parameter:F3}, Fitness: {Fitness:F2}";
    }
}
