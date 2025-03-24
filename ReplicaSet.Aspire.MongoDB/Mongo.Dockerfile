FROM library/mongo:8.0

RUN echo -n 'DummyKey' > /etc/mongo-keyfile && \
    chown -R mongodb:mongodb /etc/mongo-keyfile && \
    chmod 400 /etc/mongo-keyfile

USER mongodb

CMD ["mongod"]