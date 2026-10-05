namespace Cars24API.Models;

// A physical CARS24 touchpoint (service center, pickup point or hub)
// shown on the interactive map for a supported city.
public class Hub
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // ServiceCenter | PickupPoint | Hub
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
