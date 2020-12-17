
open Akka
open Akka.FSharp
open System
open Akka.Actor
open System.Threading;  
open System.Text; 
open Newtonsoft.Json
open System.Net.WebSockets

let mutable clientName:String = ""
let mutable registerFlag:Boolean = false
let mutable menuFlag:Boolean = true
let mutable actorFlag:Boolean = false

let socket = new ClientWebSocket()
let cts = new CancellationTokenSource()
let uri = Uri("ws://localhost:8080/websocket")

let aa = socket.ConnectAsync(uri, cts.Token)

aa.Wait()



//Create System reference
let system = System.create "system" <| Configuration.defaultConfig()

type tweet() = 
    inherit Object()

    [<DefaultValue>] val mutable id: String
    [<DefaultValue>] val mutable sender: String
    [<DefaultValue>] val mutable tweet: String
    [<DefaultValue>] val mutable mentions: List<String>
    [<DefaultValue>] val mutable hashtags: List<String>

type serverMessage() = 
    [<DefaultValue>] val mutable clientName: String
    [<DefaultValue>] val mutable command: String
    [<DefaultValue>] val mutable payload: Object

type clientMessage() = 
    [<DefaultValue>] val mutable name: String
    [<DefaultValue>] val mutable controlFlag: Boolean
    [<DefaultValue>] val mutable command: String
    [<DefaultValue>] val mutable payload: Object

type subscribedTo() =
    [<DefaultValue>] val mutable client: String
    [<DefaultValue>] val mutable subscribedClients: List<String>

type query() =
    inherit Object()

    [<DefaultValue>] val mutable typeOf: String
    [<DefaultValue>] val mutable matching: String


let clientAction clientRef controlFlag command payload =
    let clientMsg = new clientMessage()
    clientMsg.controlFlag <- controlFlag
    clientMsg.command <- command
    clientMsg.payload <- JsonConvert.SerializeObject(payload)
    let json = JsonConvert.SerializeObject(clientMsg)
    clientRef <! json

let clientAction2 clientRef controlFlag command payload =
    let clientMsg = new clientMessage()
    clientMsg.controlFlag <- controlFlag
    clientMsg.command <- command
    clientMsg.payload <- payload
    let json = JsonConvert.SerializeObject(clientMsg)
    clientRef <! json

let clientQuery client typeOf matching =
    let Client =  system.ActorSelection("akka://system/user/"+  client )
    let query = new query()
    query.typeOf <- typeOf
    query.matching <- matching
    clientAction Client true "Query" query

let clientRetweet client tweetID = 
    let Client =  system.ActorSelection("akka://system/user/"+  client )
    clientAction Client true "Retweet" tweetID

let clientLogout clientName = 
    let client =  system.ActorSelection("akka://system/user/"+  clientName )
    clientAction client true "Logout" null

let clientTweet sender tweet mentions hashtags = 
    let tweetMsg = new tweet()
    tweetMsg.id <- Guid.NewGuid().ToString()
    tweetMsg.sender <- sender
    tweetMsg.tweet <- tweet
    tweetMsg.mentions <- mentions
    tweetMsg.hashtags <- hashtags
    let client =  system.ActorSelection("akka://system/user/"+  sender )
    clientAction client true "Send Tweet" tweetMsg

let clientSubscribe client subscribeTo = 
    let Client =  system.ActorSelection("akka://system/user/"+  client )
    clientAction2 Client true "Subscribe" subscribeTo

let clientRegister client = 
    let clientRef = system.ActorSelection("akka://system/user/" + client)
    clientAction clientRef true "Register" null

let sendToServer clientName command payload=
    let serverMsg = new serverMessage()
    serverMsg.clientName <- clientName
    serverMsg.command <- command
    serverMsg.payload <- JsonConvert.SerializeObject(payload)
    let json = JsonConvert.SerializeObject(serverMsg)
    let a = Encoding.ASCII.GetBytes(json) |> ArraySegment<byte>
    let aaa = socket.SendAsync (a, WebSocketMessageType.Text, true, cts.Token)
    aaa.Wait()

    

let sendToServer2 clientName command payload=
    let serverMsg = new serverMessage()
    serverMsg.clientName <- clientName
    serverMsg.command <- command
    serverMsg.payload <- payload
    let json = JsonConvert.SerializeObject(serverMsg)
    let a = Encoding.ASCII.GetBytes(json) |> ArraySegment<byte>
    let aaa = socket.SendAsync (a, WebSocketMessageType.Text, true, cts.Token)
    aaa.Wait()





let mainMenuStart value =
    printfn "User: %A" clientName
    printfn "Main Menu"
    printfn ""
    printfn "Select Choice Below:"
    printfn "1. Register/Login"
    let choice = System.Console.ReadLine()
    choice

let mainMenu value = 
    printfn "User: %A" clientName
    printfn "Main Menu"
    printfn ""
    printfn "Select Choice Below:"
    printfn "1. Register/Login"
    printfn "2. Send Tweet"
    printfn "3. Subscribe"
    printfn "4. Retweet"
    printfn "5. Tweets I am Tagged In"
    printfn "6. Tweets from People I have Subscribed"
    printfn "7. Tweets containing a Hashtag"
    printfn "8. Logout"
    printf "Enter a number from 1-8: "
    let choice = System.Console.ReadLine()
    choice

let mainMenuRunner value = 
    if registerFlag then
        mainMenu value
    else 
        mainMenuStart value

let menuBack value = 
    printfn ""
    printfn "Press Enter to Continue"
    System.Console.ReadLine() |> ignore
    System.Console.Clear()
    menuFlag <- true

    //Actor
let Client (ClientMailbox:Actor<_>) = 
    //Actor Loop that will process a message on each iteration

    let mutable server: IActorRef = null

    let ClientToServer name server command payload = 
        if server <> null then 
            sendToServer2 name command payload
        else 
            printfn "Not Registered"

    let rec ClientLoop() = actor {

        //Receive the message
        let! msg2 = ClientMailbox.Receive()
        let msg = JsonConvert.DeserializeObject<clientMessage> msg2

        if(msg.controlFlag) then
            if(msg.command = "Register") then
                sendToServer2 ClientMailbox.Self.Path.Name "Register"ClientMailbox.Self.Path.Name
            elif (msg.command = "Send Tweet" || msg.command = "Subscribe" || 
                    msg.command = "Retweet" || msg.command = "Query" || msg.command = "Logout" ) then
                    ClientToServer ClientMailbox.Self.Path.Name server msg.command msg.payload
        else
            if(msg.command = "Register") then
                server <- ClientMailbox.Sender()
                printfn "%A Registered" ClientMailbox.Self.Path.Name
                registerFlag <- true
                menuBack true

            elif(msg.command = "Retweet") then
                printfn "%A Retweeted" ClientMailbox.Self.Path.Name
                menuBack true

            elif(msg.command = "Subscribed") then
                printfn "%A Subscribed %A" ClientMailbox.Self.Path.Name msg.payload
                menuBack true

            elif(msg.command = "Retweet") then
                printfn "%A Retweeted" ClientMailbox.Self.Path.Name
                menuBack true

            elif(msg.command = "Send Tweet") then
                printfn "%A Tweet Sent: %A" ClientMailbox.Self.Path.Name msg.payload
                menuBack true

            elif(msg.command = "MyMentions") then
                let list:List<tweet> = JsonConvert.DeserializeObject<List<tweet>> ((string)msg.payload)
                printfn "%A My Mentions Received %A" ClientMailbox.Self.Path.Name msg.payload
                menuBack true

            elif(msg.command = "Subscribed") then
                let list:List<tweet> = JsonConvert.DeserializeObject<List<tweet>> ((string)msg.payload)
                printfn "%A Subscribed Tweets Received %A" ClientMailbox.Self.Path.Name msg.payload
                menuBack true

            elif(msg.command = "Hashtags") then
                let list:List<tweet> = JsonConvert.DeserializeObject<List<tweet>> ((string)msg.payload)
                printfn "%A Hashtags Queried Returned %A" ClientMailbox.Self.Path.Name msg.payload
                menuBack true

            elif(msg.command = "Live") then 
                let liveTweet:tweet = JsonConvert.DeserializeObject<tweet> ((string)msg.payload)
                printfn "%A Live Tweet Received %A" ClientMailbox.Self.Path.Name msg.payload
                menuBack true

            elif(msg.command = "Logout") then
                registerFlag <- false
                printfn "%A Logged out!" clientName
                let client =  system.ActorSelection("akka://system/user/"+  clientName )
                menuBack true

        return! ClientLoop()
    }

    //Call to start the actor loop
    ClientLoop()

let clientSpawnRegister client = 
    if (actorFlag = false) then
        spawn system client Client |> ignore
        actorFlag <- true

    clientRegister client


let delay num = 
    System.Threading.Thread.Sleep(num * 1000)

let getListOfHashes (tweet:string) = 
    let words = tweet.Split [|' '|]
    let mutable listOfHashes = List.Empty
    for word in words do
        if ((word.Chars 0)='#') then
            listOfHashes <- listOfHashes @ [word]
    listOfHashes

let getListOfMentions (tweet:string) = 
    let words = tweet.Split [|' '|]
    let mutable listOfMentions = List.Empty
    for word in words do
        if ((word.Chars 0)='@') then
            listOfMentions <- listOfMentions @ [word.[1..word.Length-1]]
    listOfMentions

let choiceRunner choice = 
    menuFlag <- false
    if choice = "1" then
        clientSpawnRegister clientName
    elif choice = "2" && registerFlag then 
        printf "Enter Tweet: "
        let tweetText:String = System.Console.ReadLine()
        let mentionsList = getListOfMentions tweetText
        let hashtagList = getListOfHashes tweetText
        clientTweet clientName tweetText mentionsList hashtagList
    elif choice = "3" && registerFlag then
        printf "Enter User You Want to Subscribe: "
        let user2Sub:String = System.Console.ReadLine()
        clientSubscribe clientName user2Sub
    elif choice = "4" && registerFlag then
        printf "Enter Tweet ID to Retweet: "
        let tweetID:String = System.Console.ReadLine()
        clientRetweet clientName tweetID
    elif choice = "5" && registerFlag then
        clientQuery clientName "MyMentions" null
    elif choice = "6" && registerFlag then
        clientQuery clientName "Subscribed" null
    elif choice = "7" && registerFlag then
        printf "Enter Hashtag to Search: "
        let hashtag:String = System.Console.ReadLine()
        clientQuery clientName "Hashtags" hashtag
    elif choice = "8" && registerFlag then
        clientLogout clientName
    else 
        menuFlag <- true
        printfn "Invalid Input, Please Try Again"


let receivefun = async{
    let buffer = WebSocket.CreateClientBuffer(1000, 1000)
    let mutable xx = false
    let mutable dd = null
    while(true) do
        dd <- socket.ReceiveAsync (buffer, cts.Token)
        xx <- dd.Result.EndOfMessage
        let msg = JsonConvert.DeserializeObject<clientMessage> (Encoding.ASCII.GetString((Seq.toArray buffer), 0, (dd.Result.Count)))
        //printfn "%A" (Encoding.ASCII.GetString((Seq.toArray buffer), 0, (dd.Result.Count)))
        let Client =  system.ActorSelection("akka://system/user/"+  msg.name )
        Client <! (Encoding.ASCII.GetString((Seq.toArray buffer), 0, (dd.Result.Count)))
    }






let startClient = async {

    while(true) do
        if menuFlag then
            choiceRunner (mainMenuRunner true)
    
}
    
[<EntryPoint>]
let main argv =
    printf "Enter a Client Name: "
    clientName <- System.Console.ReadLine()
    printfn "Welcome %A!" clientName
    printfn ""
    printfn "Press Enter to Continue"
    System.Console.ReadLine() |> ignore
    System.Console.Clear()

    
    [receivefun; startClient]
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore
    
    System.Console.ReadKey() |> ignore

    0 // return an integer exit code