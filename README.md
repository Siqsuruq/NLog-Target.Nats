# NLog.Targets.Nats [![NuGet Release](https://img.shields.io/nuget/vpre/NLog.Targets.Nats.svg)](https://nuget.org/packages/NLog.Targets.Nats) 
NLog custom target for NATS messaging server

# Options

| Name    | Description |
|---------|-------------|
| `NatsUrl` | URL for the Nats Connecttion |
| `Subject` | Subject for the Nats message |
| `Layout`  | Payload for the Nats message |

# Example NLog.config file
```xml
<nlog>
	<extensions>
		<add assembly="NLog.Targets.Nats" />
	</extensions>

	<targets>
		<target type="Nats" name="nats" natsUrl="nats://localhost:4222" subject="logs">
			<layout>${longdate} ${level} ${message} ${exception}</layout>
		</target>
	<targets>

	<rules>
		<logger name="*" minlevel="Info" writeTo="nats" />
	</rules>
</nlog>
```