using System;

public class OutdatedDataDiscardQueue<T>
{
    private bool isBusy;

    private (T mostRecentData, Func<T, Task> mostRecentAction)? mostRecentQueueItem;

    private object queueLock = new object();

    public async Task QueueDatapoint(T data, Func<T, Task> action)
    {
        if(!isBusy)
        {
            isBusy = true;

            await action(data);

            while(mostRecentQueueItem != null)
            {
                (T mostRecentData, Func<T, Task> mostRecentAction) item;
                lock(queueLock)
                {
                    item = mostRecentQueueItem.Value;
                    this.mostRecentQueueItem = null;                                     
                }

                var (mostRecentData, mostRecentAction) = item;
                await mostRecentAction(mostRecentData);
                mostRecentQueueItem = null;
            }
          
            isBusy = false;
        }
        else
        {
            lock(queueLock)
            {
                mostRecentQueueItem = (data, action);
            }
        }
    }
}