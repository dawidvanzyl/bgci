FROM node:22-alpine

WORKDIR /app/e2e/mock-bgg
COPY *.mjs ./
COPY --from=test-data . /app/test-data/

EXPOSE 4010
CMD ["node", "server.mjs"]
