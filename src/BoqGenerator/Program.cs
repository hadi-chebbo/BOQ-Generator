if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine("Usage: dotnet run --project src/BoqGenerator -- <measurements.csv> <pricebook.csv> [output.csv]");
    return 1;
}

Console.WriteLine("BOQ generation is not implemented yet.");
return 0;
