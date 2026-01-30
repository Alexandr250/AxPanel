namespace AxPanel.UI.UserControls;

public static class FileExtensions
{
    /// <summary>
    /// Проверяет существование файла с ограничением по времени ожидания.
    /// </summary>
    /// <param name="fileInfo">Информация о файле.</param>
    /// <param name="millisecondsTimeout">Таймаут в миллисекундах.</param>
    /// <returns>True, если файл существует в течение указанного времени; иначе False.</returns>
    public static bool ExistsWithTimeout( this FileSystemInfo fileInfo, int millisecondsTimeout )
    {
        // Создаем задачу, которая выполняет синхронную проверку существования файла
        Task<bool> task = new Task<bool>( () => fileInfo.Exists );
        task.Start();
        try
        {
            // Ожидаем завершения задачи с указанным таймаутом
            if( task.Wait( millisecondsTimeout ) )
            {
                // Если задача завершилась в срок, возвращаем ее результат
                return task.Result;
            }
            else
            {
                // Если произошло превышение таймаута
                // Важно: исходная задача продолжит выполняться в фоновом режиме до своего завершения.
                // Принудительно "убить" поток сложно и не рекомендуется.
                return false;
            }
        }
        catch( AggregateException ae )
        {
            // Обработка исключений, которые могли возникнуть внутри Task (например, SecurityException)
            foreach( Exception innerException in ae.InnerExceptions )
            {
                Console.WriteLine( $"Ошибка при проверке директории: {innerException.Message}" );
            }

            return false;
        }
    }
}