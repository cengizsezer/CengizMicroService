namespace FileApiService.Api.Domain.Logging
{
    public static class LogEvents
    {
        public const int AddFileStreamError = 1001;
        public const int GetFileValidationError = 2001;
        public const int GetFileDatabaseError = 2002;
        public const int ExportFileValidationError = 3001;
        public const int ExportFileGeneralError = 3002;
    }
}
