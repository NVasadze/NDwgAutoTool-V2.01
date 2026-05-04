using System.Windows;

namespace NDwgAutoTool.Services
{
    public sealed class UiCommandRunner
    {
        private readonly Window _owner;
        private readonly Action<string> _log;
        private readonly Action<string> _setLastAction;
        private readonly Action<string> _setStatus;

        public UiCommandRunner(
            Window owner,
            Action<string> log,
            Action<string> setLastAction,
            Action<string> setStatus)
        {
            _owner = owner;
            _log = log;
            _setLastAction = setLastAction;
            _setStatus = setStatus;
        }

        public void Run(
            string actionName,
            Action operation,
            string? errorTitle = null,
            Func<Exception, string>? errorMessage = null)
        {
            _setLastAction(actionName);
            _setStatus("Working...");

            try
            {
                operation();
            }
            catch (Exception ex)
            {
                _log("ERROR: " + ex);
                StyledMessageWindow.ShowMessage(
                    errorTitle ?? $"{actionName} Error",
                    errorMessage?.Invoke(ex) ?? ex.Message,
                    _owner);
            }
            finally
            {
                _setStatus("Ready");
            }
        }

        public void RunWithResult(
            string actionName,
            Func<string> operation,
            string? successTitle = null,
            string? errorTitle = null,
            Func<Exception, string>? errorMessage = null)
        {
            Run(
                actionName,
                () =>
                {
                    string result = operation();
                    _log(result);
                    StyledMessageWindow.ShowMessage(successTitle ?? actionName, result, _owner);
                },
                errorTitle,
                errorMessage);
        }
    }
}
