IF EXISTS(SELECT * FROM sys.objects WHERE type = 'P' AND name = 'Dequeue')
    DROP PROCEDURE [SBQ].[Dequeue]
GO

CREATE PROCEDURE [SBQ].[Dequeue]
(
   @queueName AS VARCHAR(255),
   @timeout AS INT = NULL
)
AS
BEGIN
WHILE(1=1)
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
   RECEIVE TOP(1)
   @ch = conversation_handle,
   @messagetypename = message_type_name,
   @data = message_body
   FROM [SBUQ].[' + @queueName + '/queue]'

   IF( NOT @timeout IS NULL )
     SET @sql = 'WAITFOR (' + @sql + ' ), TIMEOUT '+ CAST( @timeout AS NVARCHAR )

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

   IF(@ch IS NULL)
      BREAK
   ELSE
   BEGIN
      IF (@messageTypeName = 'http://schemas.microsoft.com/SQL/ServiceBroker/EndDialog' OR @messageTypeName = 'http://schemas.microsoft.com/SQL/ServiceBroker/Error')
      BEGIN
         BEGIN TRY
            END CONVERSATION @ch
         END TRY
         BEGIN CATCH
         END CATCH
      END
      ELSE IF (@messageTypeName = 'http://schemas.microsoft.com/SQL/ServiceBroker/DialogTimer')
      BEGIN
         DECLARE @outgoingHistoryId INT
         DECLARE @conversationHandle UNIQUEIDENTIFIER
         DECLARE @messageData VARBINARY(MAX)

         DECLARE deferredMessages CURSOR FAST_FORWARD READ_ONLY FOR
         SELECT oh.[messageId], oh.[conversationHandle], oh.[data]
         FROM [SBQ].[OutgoingHistory] oh WITH(READPAST)
         JOIN sys.conversation_endpoints ce with(nolock) on ce.[conversation_handle] = oh.[conversationHandle]
         WHERE oh.[deferProcessingUntilTime] <= sysutcdatetime() AND oh.[sent] = 0

         OPEN deferredMessages

         FETCH NEXT FROM deferredMessages INTO @outgoingHistoryId, @conversationHandle, @messageData

         WHILE @@FETCH_STATUS = 0
         BEGIN

         BEGIN TRY
         BEGIN
            SEND ON CONVERSATION @conversationHandle MESSAGE TYPE [http://servicebroker.queues.com/servicebroker/2009/09/Message] (@messageData);
         END
         END TRY
         BEGIN CATCH
         END CATCH

         UPDATE [SBQ].[OutgoingHistory] SET [sent] = 1 WHERE [messageId] = @outgoingHistoryId
         FETCH NEXT FROM deferredMessages INTO @outgoingHistoryId, @conversationHandle, @messageData

         END

         CLOSE deferredMessages
         DEALLOCATE deferredMessages

         UPDATE oh SET oh.[sent] = 1
         FROM [SBQ].[OutgoingHistory] oh
         LEFT JOIN sys.conversation_endpoints ce on oh.[conversationHandle] = ce.[conversation_handle]
         WHERE ce.[conversation_handle] is null
         AND oh.[sent] = 0
      END
      ELSE
      BEGIN
         INSERT INTO [SBQ].[MessageHistory] (
         [queueName],
         [receivedTimestamp],
         [data]
         )
         VALUES(@queueName, SYSUTCDATETIME(), @data)

         SELECT @ch, @data
         BREAK
      END
      IF( NOT @timeout IS NULL )
      BREAK; -- let the client control the loop
   END
END
END
GO