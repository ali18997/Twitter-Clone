# Twitter Simulator

## Contributors:
- Syed Muhammad Ali
- Siddharth Jain

A program written as part of Distributed Operating Systems course to design and simulate the Twitter system as a web service. Used Suave IO and message passing in JSON.


## Running instructions:

Go to \Twitter Clone\Twitter Clone

### **Server:**
(Open a new terminal)
**dotnet fsi --langversion:preview ServerFinal.fsx**

### **Client:**

(Open a new terminal for every client)
**dotnet fsi --langversion:preview ClientFinal.fsx**

It will pop up a menu with instructions. Watch the demo video for the details.

## Implementation Details:

The functionality of Twitter was simulated as follows, keeping the original design as the reference. The server machine hosts many actors that distribute the work of storing and fetching tweets. We divided the load by assigning one server actor per client actor.

## Twitter Server

'startWebServer' takes a configuration record and the WebPart & starts a web server on default port 8080 over HTTP. 
We defined a function 'ws' that takes WebSocket and HttpContext typed parameters, and returns a socket computation expression:
We used the 'read' and 'send' function to receive and send messages to the clients:
We used the 'handShake' function to fit it in our web server
We maintain a hashtable 'dict' with clientID as key & the respective WebSocket as value to maintain live connections.

The server side is made up of many actors arranged in a two-tier hierarchical system. The TwitterEngine actor on top listens to requests through the websocket & routes them to the respective server actor which then processes the request & sends the response back to the respective client through the websocket. The data store consists of clients stored with their respective subscribers & tweets.

## Functionalities Implemented:

Register account
Login
Send tweet. Tweets can have hashtags (e.g. #COP5615isgreat) and mentions (@bestuser)
Subscribe to user's tweets
Re-tweets (so that your subscribers get an interesting tweet you got by other means)
Allow querying tweets subscribed to, tweets with specific hashtags, tweets in which the user is mentioned (my mentions)
If the user is connected, deliver the above types of tweets live (without querying)
Logout

## Twitter Client

Each user has its own Client actor which sends & receives messages to & from the server via websockets. On receiving any response from the websocket, the client actor prints it on the console. For message passing, the JSON format has been used as it enables lightweight messages.

**Video Presentation:** https://drive.google.com/drive/folders/1Zg_IMxuZ5u9Lu-dttmHUdzXP99RAyZEV?usp=sharing
