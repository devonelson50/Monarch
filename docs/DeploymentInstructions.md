<!-- Devon Nelson -->
# Monarch Deployment Instructions

- [Monarch Deployment Instructions](#monarch-deployment-instructions)
  - [Overview](#overview)
    - [Outbound Connectivity Requirements](#outbound-connectivity-requirements)
      - [Monapi Service Worker Container](#monapi-service-worker-container)
    - [Inbound Connectivity Requirements](#inbound-connectivity-requirements)
      - [Reverse Proxy Recommendations](#reverse-proxy-recommendations)
  - [Container Stack Configuration](#container-stack-configuration)
    - [Environment Variables](#environment-variables)
      - [monapi-worker: nagios\_uri](#monapi-worker-nagios_uri)
    - [Docker Secrets](#docker-secrets)
      - [Nagios API Key](#nagios-api-key)
      - [New Relic API Key](#new-relic-api-key)
      - [Jira API Key](#jira-api-key)
      - [OIDC Client Secret](#oidc-client-secret)
      - [Slack Webhooks](#slack-webhooks)
      - [Kafka Connection](#kafka-connection)
      - [SQL Credentials](#sql-credentials)
    - [OIDC Client Configuration](#oidc-client-configuration)
      - [Generate a Unique Client Secret](#generate-a-unique-client-secret)
      - [Set the OIDC Client Secret in Docker Secrets](#set-the-oidc-client-secret-in-docker-secrets)
      - [Update Keycloak's Configuration](#update-keycloaks-configuration)
      - [Additional Notes Regarding OIDC Configuration](#additional-notes-regarding-oidc-configuration)

## Overview
> This document includes details related to deploying Monarch in a Docker environment. 
### Outbound Connectivity Requirements
#### Monapi Service Worker Container

### Inbound Connectivity Requirements

#### Reverse Proxy Recommendations
Expand on these points:

Suggest including reverse proxy in container stack

Suggest ensuring the reverse proxy trusts mon-ca to allow
end to end encryption with full chain of trust

## Container Stack Configuration

### Environment Variables
> For any environment variable not included below, we recommend leaving the default configuration in most circumstances. The below environment variables must be set or modified.
#### monapi-worker: nagios_uri
`nagios_uri` must be set to point the `monapi` container to the intended NagiosXI instance. Any reachable NagiosXI 24 or 26 instance is supported. This variable must be set to match Nagios' external URL 1:1, including the trailing slash. Example:
`https://nxi.455garage.com/nagiosxi/`. The complete connection string(s) in `monapi`'s `NagiosConnector` object reference this environment variable such as:

``` C#
this.nagiosApiKey = File.ReadAllText("/run/secrets/monarch_nagios_api_details");
this.nagiosRequestUri = Environment.GetEnvironmentVariable("nagios_uri") ?? "";
this.nagiosRequestUri += "api/v1/objects/hoststatus?apikey=" + this.nagiosApiKey;
```

If the variable is not set, or the formatting is not correct, the integration will not function. A warning will be logged to console in the `monapi` container if the integration is unable to retrieve data from the `hoststatus` API endpoint for any reason.

> If unknown, NagiosXI's external URL can be found in the webapp: `Configure > More Options > System Configuration > System Settings > General Program Settings > External URL`. In most configurations, it should be safe to directly copy and paste this value to the `nagios_uri` environment variable.

### Docker Secrets
#### Nagios API Key
#### New Relic API Key
#### Jira API Key
#### OIDC Client Secret
#### Slack Webhooks
#### Kafka Connection
#### SQL Credentials
### OIDC Client Configuration
> This section covers the necessary steps to configure OIDC communication between the Monarch and Keycloak containers.

By default, the container stack is delivered with a default OIDC client secret. This is acceptable for testing purposes, but the secret must be replaced during deployment in a production environment.

#### Generate a Unique Client Secret
> Online client secret generation tools are available, but not recommended.

Monarch will accept any 32-byte client secret. To generate a random secret, use the generation function included in your cloud-platform of choice. In the absence of this, the following can be used to generate a random secret from your local machine:

Powershell 7:
``` Powershell
-join $(1..32 | % {(65..90 + 97..122 + 48..57 | Get-SecureRandom) | % {[char]$_ }})
```
Bash:
``` Bash
head -c 32 /dev/urandom | base64
```

#### Set the OIDC Client Secret in Docker Secrets
Set the value of the Docker Secret `monarch_oidc_client_secret` to the client secret generated in the previous step. The exact process will depend on your Docker environment.

#### Update Keycloak's Configuration
To replace the default OIDC client secret in Keycloak's configuration, open [realm.json](../keycloak/realm.json). Using the find and replace utility in your text editor, replace the single occurrence of the default secret:

Before:
```
"secret" : "uwvqb51RdR15FCYlqeBK72w9LdxDDXAN",
```

After:
```
"secret" : "<NEW OIDC CLIENT SECRET>",
```
Save the configuration, and allow the Monarch and Keycloak containers to start/restart for the changes to take effect.

#### Additional Notes Regarding OIDC Configuration
Our team initially planned to include a component to automatically generate and configure the OIDC client secret based on the value stored in the Docker Secret. While it is possible to import a client secret into the Keycloak configuration using Docker Secrets and Keycloak's API, this does not eliminate the issue of Keycloak's native configuration export including the client secret in plaintext. Because of this behavior, any future changes to Keycloak's configuration will once again leak the client secret. Implementing this automation would result in a portion of our code deliberately writing a credential to a json in plaintext. With this in mind, we have determined it is best to prioritize properly handling the client secret in the rest of the container stack to ensure the issue can be completely remediated by removing Keycloak from the container stack. As mentioned in our Risk-Based Information Security Analysis, we highly recommend configuring Monarch to interact directly with your Identity Provider of choice instead of Keycloak.

