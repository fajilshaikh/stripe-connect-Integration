namespace Core.Entities;

/// Seller: Jyrney is merchant of record (charge on platform).
/// Agent: supplier is settlement merchant (destination charge with on_behalf_of, Jyrney keeps commission as application fee).
public enum CommercialModel
{
    Seller,
    Agent
}
