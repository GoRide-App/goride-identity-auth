namespace SRC.Dtos;

public class DeactivateAccountRequestDto
{
    /// <summary>Must be true. The UI shows a confirmation dialog; this is the server-side proof of it.</summary>
    public bool Confirm { get; set; }
}
