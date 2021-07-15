using System.Threading.Tasks;

namespace hypixel
{
    public class UnsubFlipperCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            Flipper.FlipperEngine.Instance.RemoveNonConnection(con);
            return data.Ok();
        }
    }
}