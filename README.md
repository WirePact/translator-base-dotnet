# WirePact Translator Base in dotnet

This represents the base API to communicate with WirePact / Envoy. The project (library) contains the needed gRPC files
and services to enable the external auth communication with Envoy.

To build a translator, install this package as a dependency and create a gRPC service that
inherits `Authorization.AuthorizationBase`. Then, overwrite the `Check` method.

To test such an external auth element locally with an Envoy, configure envoy to communicate with the service over gRPC:

```yaml
# other config
# (snip.)

static_resources:
  listeners:
    - name: listener_0
      address:
        socket_address:
          protocol: TCP
          address: 0.0.0.0
          port_value: 8080
      filter_chains:
        - filters:
            - name: envoy.filters.network.http_connection_manager
              typed_config:
                # target service config...
                # (snip)
                http_filters:
                  - name: envoy.filters.http.ext_authz
                    typed_config:
                      '@type': type.googleapis.com/envoy.extensions.filters.http.ext_authz.v3.ExtAuthz
                      transport_api_version: v3
                      grpc_service:
                        envoy_grpc:
                          cluster_name: auth_translator
                        # Default is 200ms; override if your server needs e.g. warmup time.
                        # dotnet in development mode needs some time.
                        timeout: 1s
                      include_peer_certificate: true
                  - name: envoy.filters.http.router
  clusters:
    - name: auth_translator
      connect_timeout: 0.5s
      type: STRICT_DNS
      typed_extension_protocol_options:
        envoy.extensions.upstreams.http.v3.HttpProtocolOptions:
          '@type': type.googleapis.com/envoy.extensions.upstreams.http.v3.HttpProtocolOptions
          explicit_http_config:
            http2_protocol_options: { } # to enable h2 for gRPC
      load_assignment:
        cluster_name: auth_translator
        endpoints:
          - lb_endpoints:
              - endpoint:
                  address:
                    socket_address:
                      address: translator # your service
                      port_value: 1337 # the configured port
```
