# Duplicati Console Scheduler

This project is a simple scheduler that injects messages into the console with a regular interval.

The project also doubles as a data initializer, such that this project is able to initialize a PostGres database for use with MassTransit communication.

For development purposes, the project can start without any connection strings, but it will not use a database for persisting the quartz state and not communicate with a message bus.

This service is currently self-contained and does not accept any external commands, but still exposes a web server. This web server interface should not be exposed.

## Environment variables

| Variable                              | Description                                                                    |
| ------------------------------------- | ------------------------------------------------------------------------------ |
| ENVIRONMENT\_\_REDIRECTURL            | URL used to redict requests to the root                                        |
| DATABASE\_\_ADMINCONNECTIONSTRING     | Connectionstring for updating the schema used by Quartz                        |
| DATABASE\_\_CONNECTIONSTRING          | Connectionstring for persisting the state used by Quartz                       |
| MESSAGING\_\_ADMINCONNECTIONSTRING    | Connectionstring for updating the schema for the messaging bus                 |
| MESSAGING\_\_CONNECTIONSTRING         | Connectionstring for communicating over the message bus                        |
| SERILOG\_\_SOURCETOKEN                | The token used to log data from serilog                                        |
| SERILOG\_\_ENDPOINT                   | The serilog endpoint to send logs to                                           |
| SECURITY\_\_MAXREQUESTSPERSECONDPERIP | The maximum number of request from a single IP per second before throttling it |
| SECURITY\_\_FILTERPATTERNS            | Boolean toggling filtering of scanning patterns                                |
| SECURITY\_\_RATELIMITENABLED          | Boolean toggling if IP rate limiting is enabled                                |

## Configure local environment for a message bus

It is recommeded that you configure variables by creating a [`local.environmentvariables.json`](./src/Scheduler/local.environmentvariables.json) file in the project folder. This file is excluded from Git and Docker, making it less likely that you accidentally leak test variables.
