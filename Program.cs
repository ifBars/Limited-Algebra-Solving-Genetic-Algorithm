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
        int populationSize = 0;
        int generations = 0;

        double mutationRate;
        double crossoverRate;

        string equation;
        string[] sides;

        Console.WriteLine("Choose a mode");
        Console.WriteLine("[1] Simple");
        Console.WriteLine("[2] Advanced");
        Console.Write("->");
        string readChoice = Console.ReadLine();
        int choice;

        if (!int.TryParse(readChoice, out choice))
        {
            Console.WriteLine("Invalid choice... Press any key to go back...");
            Console.ReadKey();
            Console.Clear();
            Main();
        }

        Console.WriteLine("Enter the algebraic equation to be solved (refer to example equations txt file):");
        equation = Console.ReadLine();

        sides = equation.Split('=');
        if (sides.Length != 2)
        {
            Console.WriteLine("Invalid equation format.");
            Console.ReadKey();
            Console.Clear();
            Main();
        }

        if (!double.TryParse(sides[1], out expectedSolution))
        {
            Console.WriteLine("Invalid expected solution format.");
            Console.ReadKey();
            Console.Clear();
            Main();
        }

        if (choice == 2)
        {
            Console.WriteLine("Enter the desired population size per generation:");
            populationSize = int.Parse(Console.ReadLine());

            Console.WriteLine("Enter the desired number of generations:");
            generations = int.Parse(Console.ReadLine());

            Console.WriteLine("Enter the desired mutation rate:");
            mutationRate = double.Parse(Console.ReadLine());

            Console.WriteLine("Enter the desired crossover rate:");
            crossoverRate = double.Parse(Console.ReadLine());
        }
        else
        {
            Console.WriteLine("Enter the desired total population size:");
            int totalDesiredSize = int.Parse(Console.ReadLine());
            mutationRate = 0.3;
            crossoverRate = 0.8;

            int range = (int)Math.Sqrt(totalDesiredSize);

            // Iterate through possible values for populationSize
            for (populationSize = 1; populationSize <= range; populationSize++)
            {
                // Calculate generations based on populationSize
                generations = totalDesiredSize / populationSize;
            }
        }

        List<Solution> totalPopulation = new List<Solution>();
        List<Solution> population = InitializePopulation(populationSize);

        int counter = 0;
        object lockObject = new object();

        // Use parallelism to run each generation on a seperate thread
        Parallel.For(0, generations, generation =>
        {
            Solution bestSolution = null;

            bestSolution = Train(population, generation, equation);

            if (population.Count == 0)
            {
                // Console.WriteLine($"Repopulating the population for generation {generation + 1}.");
                population = InitializePopulation(populationSize);

                bestSolution = Train(population, generation, equation);
            }

            List<Solution> selected = Select(population);
            List<Solution> newPopulation = Crossover(selected, crossoverRate);
            Mutate(newPopulation, mutationRate);

            // Use lock to ensure that only one thread can modify the lists at a time, to avoid race conditions
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
            Console.WriteLine($"Given Equation: {equation}");
            Console.WriteLine($"Total Population Size: {totalSize}");
            Console.WriteLine($"Total Living Population Size: {totalPopulation.Count}");
            Console.WriteLine($"Generation Population Size: {populationSize}");
            Console.WriteLine($"Total Generations: {generations}");
            Console.WriteLine($"Total Mutations: {mutations}");
            Console.WriteLine($"Total Crossovers: {crossovers}");
            Console.WriteLine($"Final Best Solution Found: {finalBestSolution.Parameter}");
            Console.WriteLine($"Best Solution's Equation Result: {finalBestSolution.Result}");
            Console.WriteLine($"Expected Solution: {expectedSolution}");
            Console.WriteLine("");
            Console.WriteLine("Would you like to run another? (y / n)");
            string input = Console.ReadLine();
            if (input == "y" || input == "Y")
            {
                Console.Clear();
                Main();
            }
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
            // Console.WriteLine($"Generation {generation + 1}: Best solution - {bestSolution}");
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

                // Print the parameter and fitness value for debugging
                // Console.WriteLine($"Parameter: {solution.Parameter}, Fitness: {solution.Fitness}");
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
