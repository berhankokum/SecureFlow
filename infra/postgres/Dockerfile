FROM golang:1.26-alpine@sha256:ce864e7223ac17b1775e6fd0b4c0db580c2eb50e7953a427916379e4b92a1628 AS gosu
ENV CGO_ENABLED=0 GOTOOLCHAIN=local
RUN go install github.com/tianon/gosu@v0.0.0-20250923190938-6456aaa0f3c8 && go version -m /go/bin/gosu

FROM postgres:17-alpine@sha256:18cfe3ef5e6815560c98237d6216d1e5119702fb0f3894c8785dd58b8bbe5d73
# Fail closed if the reviewed patch versions disappear; review pins rather than
# silently falling back to vulnerable packages. Re-scan every resulting image.
RUN apk add --no-cache --upgrade \
    libapk=3.0.8-r0 apk-tools=3.0.8-r0 xz-libs=5.8.4-r0 \
    libcrypto3=3.5.8-r0 libssl3=3.5.8-r0 libuuid=2.42.3-r1 tzdata=2026d-r0 \
    ca-certificates-bundle=20260909-r0
COPY --from=gosu /go/bin/gosu /usr/local/bin/gosu
RUN gosu nobody id && postgres --version
