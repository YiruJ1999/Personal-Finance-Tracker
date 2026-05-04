// Personal Finance Tracker
// File: PersonalFinanceTracker/Services/IErrorHandler.cs
// Purpose: Provides an application service shared across repositories and page models.

namespace PersonalFinanceTracker.Services
{
    /// <summary>
    /// Error Handler Service.
    /// </summary>
    public interface IErrorHandler
    {
        /// <summary>
        /// Handle error in UI.
        /// </summary>
        /// <param name="ex">Exception being thrown.</param>
        void HandleError(Exception ex);
    }
}
