<!-- Devon Nelson -->
# Monarch Deployment Instructions

- [Monarch Deployment Instructions](#monarch-deployment-instructions)
  - [Connection Requirements](#connection-requirements)
    - [Outbound Connectivity Requirements](#outbound-connectivity-requirements)
    - [Inbound Connectivity Requirements](#inbound-connectivity-requirements)
    - [Certificate Trust Requirements](#certificate-trust-requirements)
  - [Environment Variables](#environment-variables)
    - [monapi-worker: nagios\_uri](#monapi-worker-nagios_uri)
    - [monapi-worker: kafka\_server](#monapi-worker-kafka_server)
    - [monapi-worker: kafka\_port](#monapi-worker-kafka_port)
    - [monapi-worker: kafak\_user](#monapi-worker-kafak_user)
  - [Docker Secrets](#docker-secrets)
    - [Nagios API Key](#nagios-api-key)
    - [New Relic API Key](#new-relic-api-key)
    - [Jira API Key](#jira-api-key)
    - [OIDC Client Secret](#oidc-client-secret)
    - [Slack Webhooks](#slack-webhooks)
    - [Kafka Password](#kafka-password)
    - [SQL Credentials](#sql-credentials)
  - [OIDC Client Configuration](#oidc-client-configuration)
    - [Generate a Unique Client Secret](#generate-a-unique-client-secret)
    - [Set the OIDC Client Secret in Docker Secrets](#set-the-oidc-client-secret-in-docker-secrets)
    - [Update Keycloak's Configuration](#update-keycloaks-configuration)
    - [Additional Notes Regarding OIDC Configuration](#additional-notes-regarding-oidc-configuration)
  - [Starting the Container Stack](#starting-the-container-stack)
    - [mon-ca](#mon-ca)
    - [monarch-certificate-provider](#monarch-certificate-provider)
    - [sqlserver](#sqlserver)
    - [monapi](#monapi)

## Connection Requirements
### Outbound Connectivity Requirements
> This section describes the minimum required outbound connection requirements for Monarch to function. If your host is not subject to strict outbound connection whitelisting, this section can likely be skipped.

| Service | FQDN | Protocol | Destination Port | Description |
|---------|------|----------|------------------|-------------|
| New Relic | api.newrelic.com | TCP | 443 | Retrieve monitoring data |
| Nagios | {Your Nagios Host} | TCP | 443 | Retrieve monitoring data |
| Jira | {yourdomain}.atlassian.net | TCP | 443 | Create Jira Issues |
| Slack | hooks.slack.com | TCP | 443 | Send Slack alerts via webhook(s) |
| DNS | * | UDP | 53 | Upstream DNS from host for external name resolution |
| NTP | * | UDP | 123 | Upstream NTP from host for time synchronization |

The stack is delivered without the connection between `keycloak` and an external identity provider having been established. Please refer to your identity provider's documentation to for notes regarding their outbound connectivity requirements.

Accurate time synchronization is required to ensure monitoring data and notifications are properly handled, and for public certificate verification.

Whitelisting the required connections for relevant Online Certificate Status Protocol (OCSP), Certificate Trust List (CTL), and Certificate Revocation List (CRL) is strongly recommended.

### Inbound Connectivity Requirements
> This section describes the required connections that must be made accessible to end users. The listening port describes the port the connection has been mapped to on the host.

| Service | Protocol | Listening Port | Description |
|---------|----------|----------------|-------------|
| Monarch | TCP | 443 | Monarch Web Application |
| Keycloak | TCP | 8443 | User Authentication |

> By default the `sqlserver` is not exposed outside of the container stack. If necessary for troubleshooting, the port can be temporarily exposed by modifying `compose.yaml`.

### Certificate Trust Requirements
Monarch uses a self-signed certificate authority to allow all internal and end-user-facing connections to be encrypted with certificate verification, without the need to expose additional ports for automatic renewals through a public certificate authority. The root certificate is automatically regenerated anytime `mon-ca`'s volume is reset.

We recommend installing `monarch_root_ca.crt` in the root certificate store of the user-facing reverse proxy. This will ensure all end-user traffic takes place across encrypted, verified connections from end-to-end. Anytime `mon-ca`'s volume is reset, the root certificate will need to be re-imported into the reverse proxy's certificate store.

> Monarch ships with a default `monarch_root_ca.crt`. This will automatically regenerate on the first execution of the container stack to ensure the certificate chain remains unique across Monarch instances should more than one be deployed. During deployment, be sure to import the newly generated root certificate after the first execution, rather than the default. If you import the default root certificate, we recommend removing it. Allow exported root certificates to be overwritten, rather than manually deleting them. If the certificate is manually deleted Docker may attempt to create a directory after failing to mount the certificate file.

Unique end-entity certificates are generated for all services that listen to incoming traffic from clients, or on the internal Docker network. End-entity certificates expire every 24 hours, and are automatically regenerated each time `monarch-certificate-provider` is started. We recommend restarting the entire container stack every 24 hours to ensure all services apply the updated certificates. Automatic container stack restarts can be scheduled outside of business hours using crontab, or similar. Exact setup details may vary by hosting platform.

## Environment Variables
> For any environment variable not included below, we recommend leaving the default configuration in most circumstances. The below environment variables must be set or modified.
### monapi-worker: nagios_uri
`nagios_uri` must be set to point the `monapi` container to the intended NagiosXI instance. Any reachable NagiosXI 24 or 26 instance is supported. This variable must be set to match Nagios' external URL 1:1, including the trailing slash. Example:
`https://nxi.455garage.com/nagiosxi/`. The complete connection string(s) in `monapi`'s `NagiosConnector` object reference this environment variable such as:

``` C#
this.nagiosApiKey = File.ReadAllText("/run/secrets/monarch_nagios_api_details");
this.nagiosRequestUri = Environment.GetEnvironmentVariable("nagios_uri") ?? "";
this.nagiosRequestUri += "api/v1/objects/hoststatus?apikey=" + this.nagiosApiKey;
```

If the variable is not set, or the formatting is not correct, the integration will not function. A warning will be logged to console in the `monapi` container if the integration is unable to retrieve data from the `hoststatus` API endpoint for any reason.

> If unknown, NagiosXI's external URL can be found in the webapp: `Configure > More Options > System Configuration > System Settings > General Program Settings > External URL`. In most configurations, it should be safe to directly copy and paste this value to the `nagios_uri` environment variable.

### monapi-worker: kafka_server
The Kafka integration is off by default. The integration is activated by populating this variable. `kafka_server` should point to the FQDN of your desired Kafka broker.

Our current implementation assumes the broker will offer an encrypted connection and present a certificate signed by a publicly trusted certificate authority.

### monapi-worker: kafka_port
`kafka_port` will accept the broker's listening port. This variable must be populated, even if the standard TLS port is in use.
### monapi-worker: kafak_user
`kafka_user` will accept the intended user name.

## Docker Secrets
> Monarch will be delivered with all Docker Secrets configured for injection from an external source.

### Nagios API Key
This secret should exactly match the API key retrieved from your Nagios instance.

### New Relic API Key
This secret should exactly match the API key retrieved from your New Relic instance.

### Jira API Key
THis secret should contain the API key retrieved from your Jira instance, and should follow the below format:
```
example@email.com:<api-key>
```
### OIDC Client Secret
This secret must be populated with the OIDC Client Secret generated [here].(#generate-a-unique-client-secret)
### Slack Webhooks
This secret will accept one or more webhooks stored in JSON format as shown below. The key for each webhook will be used as its display name in the Administrative Panel.
```json
{
  "supportTeam": "https://hooks.slack.com/services/...",
  "networkTeam": "https://hooks.slack.com/services/...",
  "SOCteam": "https://hooks.slack.com/services/..." 
}
```
### Kafka Password 
This secret should exactly match the password for the account used to connect to your Kafka broker. The default configuration of our Kafka producer expects SaslSsl authentication. If certificate authentication is needed in the future, this secret could be repurposed with slight adjustments to `monapi`'s Kafka connector.

### SQL Credentials
Each SQL credential should be unique, and is subject to Microsoft SQL Server's default length and complexity requirements. Accounts are in place for each service following the principle of least privilege.

## OIDC Client Configuration
> This section covers the necessary steps to configure OIDC communication between the Monarch and Keycloak containers.

The container stack is delivered with a default OIDC client secret. This is acceptable for testing purposes, but the secret must be replaced during deployment.

### Generate a Unique Client Secret
Monarch will accept any 32-byte client secret. To generate a random secret, use the generation function included in your cloud-platform of choice. In the absence of this, the following can be used to generate a random secret from your local machine:

Powershell 7:
``` Powershell
-join $(1..32 | % {(65..90 + 97..122 + 48..57 | Get-SecureRandom) | % {[char]$_ }})
```
Bash:
``` Bash
head -c 32 /dev/urandom | base64
```

### Set the OIDC Client Secret in Docker Secrets
Set the value of the Docker Secret `monarch_oidc_client_secret` to the client secret generated in the previous step. The exact process will depend on your Docker environment.

### Update Keycloak's Configuration
To replace the default OIDC client secret in Keycloak's configuration, open [realm.json](../keycloak/realm.json). Using the find and replace utility in your text editor, replace the single occurrence of the default secret:

Before:
```json
"secret" : "uwvqb51RdR15FCYlqeBK72w9LdxDDXAN",
```

After:
```json
"secret" : "<NEW OIDC CLIENT SECRET>",
```
Save the configuration, and allow the Monarch and Keycloak containers to start/restart for the changes to take effect.

### Additional Notes Regarding OIDC Configuration
Our team initially planned to include a component to automatically generate and configure the OIDC client secret based on the value stored in the Docker Secret. While it is possible to import a client secret into the Keycloak configuration using Docker Secrets and Keycloak's API, this does not eliminate the issue of Keycloak's native configuration export including the client secret in plaintext. Because of this behavior, any future changes to Keycloak's configuration will once again leak the client secret. Implementing this automation would result in a portion of our code deliberately writing a credential to a json in plaintext. With this in mind, we have determined it is best to prioritize properly handling the client secret in the rest of the container stack to ensure the issue can be completely remediated by removing Keycloak from the container stack. As mentioned in our Risk-Based Information Security Analysis, we highly recommend configuring Monarch to interact directly with your Identity Provider of choice instead of Keycloak.

## Starting the Container Stack
Monarch will automatically launch in order of its dependent services, which is as follows:

### mon-ca
`mon-ca` is host to the local certificate authority. This container must start first to prepare for the process of automatically generating and applying certificates.

### monarch-certificate-provider
Once `mon-ca` is ready, this container will start in order to execute `cert-provider.sh`. This script communicates with the certificate authority to accomplish the following:
- Generate unique end-entity certificates for each service
- Deploy end-entity certificates automatically using Docker Volumes
- Distribute root, or full-chain certificates as needed to ensure each service in the stack can communicate without skipping certificate verification
- Export the root certificate for access by the reverse proxy

On some UNIX hosts, shell scripts will follow the host-level permissions when mounted as a volume. Execute permissions have been set on `cert-provider.sh`. In the event thsi permission did not reflect on your new host, it may be necessary to update the permissions using:
```bash
chmod +x cert-provider.sh
```

### sqlserver
After `cert-provider.sh` has successfully exited, `sqlserver`, the final dependency for all remaining services, will start. `sqlserver` uses a modified entrypoint script that allows automatic initialization, or reinitialization of the database. This initialization process prepares the necessary users, and applies Monarch's schema.

On some UNIX hosts, shell scripts will follow the host-level permissions when mounted as a volume. Execute permissions have been set on both scripts in `initdb`. In the event thsi permission did not reflect on your new host, it may be necessary to update the permissions using:
```bash
chmod +x initdb/*
```

### monapi
