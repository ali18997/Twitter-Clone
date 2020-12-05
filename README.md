# Twitter-Clone

1)Team members

Siddharth Jain 9881-8991 Syed Muhammad Ali 3816-1305

2)RUNNING INSTRUCTIONS:
 
Go to \Twitter Clone\Twitter Clone
Run the Server.fsx file as: dotnet fsi --langversion:preview ServerPerformance.fsx
Run the Client.fsx file as: dotnet fsi --langversion:preview ClientPerformance.fsx <no_of_clients> <no_of_requests> eg. dotnet fsi --langversion:preview ClientPerformance.fsx 2000 2000


3)What is working?
Register account
Send a tweet. Tweets can have hashtags and mentions.
Subscribe to user's tweets
Re-tweets 
Query tweets subscribed to, tweets with specific hashtags, tweets in which the user is mentioned
If the user is connected, the tweets are delivered live (without querying)


4)Performance
Refer report
