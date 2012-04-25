IF EXISTS(SELECT * FROM sys.objects WHERE type = 'P' AND name = 'Peek')
    DROP PROCEDURE [SBQ].[Peek]
GO

CREATE PROCEDURE [SBQ].[Peek]
(
   @queueName AS VARCHAR(255)
)
AS
BEGIN
   DECLARE @ch UNIQUEIDENTIFIER = NULL
   DECLARE @messageTypeName VARCHAR(256)
   DECLARE @data VARBINARY(MAX)
   DECLARE @sql NVARCHAR(MAX)
   DECLARE @param_def NVARCHAR(MAX)

   -- Creating the parameter substitution
   SET @param_def = '
   @ch UNIQUEIDENTIFIER OUTPUT,
   @messagetypename VARCHAR(256) OUTPUT,
   @data VARBINARY(MAX) OUTPUT'

   SET @sql = '
   SELECT TOP(1)
   @ch = conversation_handle,
   @messagetypename = message_type_name,
   @data = message_body
   FROM [SBUQ].[' + @queueName + '/queue] WITH (NOLOCK)'

   BEGIN TRY
   EXEC sp_executesql
      @sql,
      @param_def,
      @ch = @ch OUTPUT,
      @messageTypeName = @messagetypename OUTPUT,
      @data = @data OUTPUT
   END TRY
   BEGIN CATCH
   END CATCH

   SELECT @ch, @data
END
GO

UPDATE [SBQ].[Detail] SET schemaVersion='1.1';