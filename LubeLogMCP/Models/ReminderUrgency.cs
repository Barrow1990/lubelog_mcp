namespace LubeLogMCP.Models
{
    // Matches lubelog's own ReminderUrgency enum by name; it is sent as a query-string value.
    public enum ReminderUrgency
    {
        NotUrgent = 0,
        Urgent = 1,
        VeryUrgent = 2,
        PastDue = 3
    }
}
