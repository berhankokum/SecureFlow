# Evaluation only; not wired into Compose/CI. See ADR-024.
FROM golang:1.26-alpine@sha256:ce864e7223ac17b1775e6fd0b4c0db580c2eb50e7953a427916379e4b92a1628 AS gosu
ENV CGO_ENABLED=0 GOTOOLCHAIN=local
RUN go install github.com/tianon/gosu@v0.0.0-20250923190938-6456aaa0f3c8 && go version -m /go/bin/gosu

FROM postgres:17-alpine@sha256:18cfe3ef5e6815560c98237d6216d1e5119702fb0f3894c8785dd58b8bbe5d73
# Repository versions are deliberately measured during evaluation. Before promotion,
# lock resolved versions and archive the final image by digest for reproducibility.
RUN apk upgrade --no-cache
COPY --from=gosu /go/bin/gosu /usr/local/bin/gosu
RUN gosu nobody id && postgres --version
