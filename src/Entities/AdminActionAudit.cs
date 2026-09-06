using SRC.Enums;

namespace SRC.Entities;

public class AdminActionAudit
{
    public Guid Id{get; set;}
    public string ActorId {get; set;} = default!;
    public AdminActionType Action {get; set;}
    public string TargetId {get; set;} = default!;
    public DateTime TimeStampUtc {get;set;}
}